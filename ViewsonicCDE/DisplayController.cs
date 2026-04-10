using System;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace ViewsonicCDE
{
    public class DisplayController
    {
        // --- Constants ---
        private const int HEARTBEAT_INTERVAL_MS = 45000;
        private const int RECONNECT_BASE_MS = 5000;
        private const int RECONNECT_MAX_MS = 60000;
        private const int MAX_BUFFER_SIZE = 1024;
        private const int CMD_TIMEOUT_MS = 3000;

        // --- Infrastructure ---
        private TCPClient _tcpClient;
        private CTimer _heartbeatTimer;
        private CTimer _reconnectTimer;
        private CTimer _queueTimer; // Handles both command timeouts and intended delays

        private string _rxBuffer = "";
        private int _currentBackoffMs = RECONNECT_BASE_MS;
        private bool _enableConnection = false;

        private readonly object _bufferLock = new object();
        private readonly object _queueLock = new object();
        private readonly object _timerLock = new object();

        // --- Command Queue Infrastructure ---
        private class QueuedCmd
        {
            public string CommandString;
            public int CmdType; // 0=Raw/Get, 1=Power, 2=Input, 3=Volume
            public ushort PendingValue;
            public bool IsDelay;
            public int DelayMs;
        }

        private Queue<QueuedCmd> _cmdQueue = new Queue<QueuedCmd>();
        private QueuedCmd _activeCmd = null;
        private bool _isWaitingForReply = false;

        // --- Delegates ---
        public delegate void StateChangeHandler(ushort state);
        public StateChangeHandler OnConnectionChange { get; set; }
        public StateChangeHandler OnPowerChange { get; set; }
        public StateChangeHandler OnInputChange { get; set; }
        public StateChangeHandler OnVolumeChange { get; set; }

        // =========================================================================
        // CONNECTION LIFECYCLE
        // =========================================================================
        public void Initialize(string ipAddress, ushort port)
        {
            try
            {
                _enableConnection = true;
                CleanupClient(); // Properly disposes old client before creating a new one

                _tcpClient = new TCPClient(ipAddress, port, 4096);
                _tcpClient.SocketStatusChange += OnSocketStatusChange;
                _tcpClient.ConnectToServerAsync(ConnectCallback);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("ViewsonicCDE Init Error: " + ex.Message);
            }
        }

        public void Disconnect()
        {
            _enableConnection = false;
            StopAllTimers();
            CleanupClient();

            lock (_queueLock)
            {
                _cmdQueue.Clear();
                _activeCmd = null;
                _isWaitingForReply = false;
            }
        }

        private void CleanupClient()
        {
            if (_tcpClient != null)
            {
                _tcpClient.SocketStatusChange -= OnSocketStatusChange;
                if (_tcpClient.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                    _tcpClient.DisconnectFromServer();
                _tcpClient.Dispose();
                _tcpClient = null;
            }
        }

        private void ConnectCallback(TCPClient client)
        {
            if (client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                _currentBackoffMs = RECONNECT_BASE_MS; // Reset backoff on success
                client.ReceiveDataAsync(ReceiveCallback);

                PollPower();
                PollInput();
                PollVolume();
            }
        }

        private void OnSocketStatusChange(TCPClient client, SocketStatus clientSocketStatus)
        {
            if (clientSocketStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                OnConnectionChange?.Invoke(1);

                lock (_timerLock)
                {
                    StopTimer(ref _reconnectTimer);
                    _heartbeatTimer = new CTimer(HeartbeatCallback, null, HEARTBEAT_INTERVAL_MS, HEARTBEAT_INTERVAL_MS);
                }
            }
            else
            {
                OnConnectionChange?.Invoke(0);
                StopTimer(ref _heartbeatTimer);

                lock (_queueLock)
                {
                    _cmdQueue.Clear();
                    _isWaitingForReply = false;
                    _activeCmd = null;
                }

                if (_enableConnection)
                {
                    lock (_timerLock)
                    {
                        if (_reconnectTimer == null) // Prevent stacking
                        {
                            _reconnectTimer = new CTimer(ReconnectCallback, null, _currentBackoffMs);
                        }
                    }
                }
            }
        }

        private void ReconnectCallback(object userSpecific)
        {
            lock (_timerLock) { StopTimer(ref _reconnectTimer); }

            // Print the attempt to the Crestron Error Log / Console
            ErrorLog.Notice(string.Format("ViewsonicCDE: Attempting reconnect... Next backoff will be {0}ms", _currentBackoffMs));

            // Exponential Backoff calculation
            _currentBackoffMs = Math.Min(_currentBackoffMs * 2, RECONNECT_MAX_MS);

            if (_enableConnection && _tcpClient != null && _tcpClient.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                _tcpClient.ConnectToServerAsync(ConnectCallback);
            }
        }

        // =========================================================================
        // TIMERS & HEARTBEAT
        // =========================================================================
        private void StopTimer(ref CTimer timer)
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }
        }

        private void StopAllTimers()
        {
            lock (_timerLock)
            {
                StopTimer(ref _heartbeatTimer);
                StopTimer(ref _reconnectTimer);
                StopTimer(ref _queueTimer);
            }
        }

        private void HeartbeatCallback(object userSpecific)
        {
            // Polls all states to prevent drift
            PollPower();
            PollInput();
            PollVolume();
        }

        // =========================================================================
        // COMMAND QUEUE & TRANSMISSION
        // =========================================================================
        private void EnqueueCommand(string command, int type, ushort val)
        {
            lock (_queueLock)
            {
                _cmdQueue.Enqueue(new QueuedCmd { CommandString = command, CmdType = type, PendingValue = val, IsDelay = false });
            }
            ProcessQueue();
        }

        private void EnqueueDelay(int delayMs)
        {
            lock (_queueLock)
            {
                _cmdQueue.Enqueue(new QueuedCmd { IsDelay = true, DelayMs = delayMs });
            }
            ProcessQueue();
        }

        private void ProcessQueue()
        {
            lock (_queueLock)
            {
                if (_isWaitingForReply || _cmdQueue.Count == 0) return;

                _activeCmd = _cmdQueue.Dequeue();
                _isWaitingForReply = true;

                if (_activeCmd.IsDelay)
                {
                    lock (_timerLock)
                    {
                        StopTimer(ref _queueTimer);
                        _queueTimer = new CTimer(QueueTimeoutCallback, null, _activeCmd.DelayMs);
                    }
                }
                else
                {
                    try
                    {
                        if (_tcpClient != null && _tcpClient.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                        {
                            byte[] bytes = Encoding.ASCII.GetBytes(_activeCmd.CommandString);
                            _tcpClient.SendDataAsync(bytes, bytes.Length, SendCallback);

                            lock (_timerLock)
                            {
                                StopTimer(ref _queueTimer);
                                _queueTimer = new CTimer(QueueTimeoutCallback, null, CMD_TIMEOUT_MS);
                            }
                        }
                        else
                        {
                            ClearActiveCommand(); // Socket dead, dump command
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorLog.Error("ViewsonicCDE Send Error: " + ex.Message);
                        ClearActiveCommand();
                    }
                }
            }
        }

        private void QueueTimeoutCallback(object specific)
        {
            lock (_queueLock)
            {
                ClearActiveCommand(); // Timeout reached or Delay finished, move to next
            }
            ProcessQueue();
        }

        private void ClearActiveCommand()
        {
            _isWaitingForReply = false;
            _activeCmd = null;
            lock (_timerLock) { StopTimer(ref _queueTimer); }
        }

        private void SendCallback(TCPClient client, int numberOfBytesSent) { }

        // =========================================================================
        // RECEPTION & PARSING
        // =========================================================================
        private void ReceiveCallback(TCPClient client, int numberOfBytesReceived)
        {
            if (numberOfBytesReceived > 0)
            {
                string incoming = Encoding.ASCII.GetString(client.IncomingDataBuffer, 0, numberOfBytesReceived);

                lock (_bufferLock)
                {
                    _rxBuffer += incoming;

                    // Safe Trim: Find the last complete message boundary
                    if (_rxBuffer.Length > MAX_BUFFER_SIZE)
                    {
                        int lastCr = _rxBuffer.LastIndexOf('\x0D');
                        if (lastCr >= 0 && lastCr < _rxBuffer.Length - 1)
                            _rxBuffer = _rxBuffer.Substring(lastCr + 1);
                        else
                            _rxBuffer = ""; // No CR found in massive buffer, dump corrupt data

                        ErrorLog.Notice("ViewsonicCDE: Buffer trimmed to prevent overflow.");
                    }

                    ParseBuffer();
                }

                client.ReceiveDataAsync(ReceiveCallback);
            }
        }

        private void ParseBuffer()
        {
            int crPos = _rxBuffer.IndexOf('\x0D');
            while (crPos >= 0)
            {
                string msg = _rxBuffer.Substring(0, crPos);
                _rxBuffer = _rxBuffer.Substring(crPos + 1);

                // Variables for safe delegate invocation
                Action safeInvoke = null;

                if (msg.Length >= 4) // Validation
                {
                    char typeChar = msg[3];

                    // --- ACK Handling ---
                    if (typeChar == '+')
                    {
                        lock (_queueLock)
                        {
                            if (_activeCmd != null && _activeCmd.CmdType != 0)
                            {
                                ushort val = _activeCmd.PendingValue;
                                if (_activeCmd.CmdType == 1) safeInvoke = () => OnPowerChange?.Invoke(val);
                                else if (_activeCmd.CmdType == 2) safeInvoke = () => OnInputChange?.Invoke(val);
                                else if (_activeCmd.CmdType == 3) safeInvoke = () => OnVolumeChange?.Invoke(val);
                            }
                            ClearActiveCommand(); // Command acknowledged, advance queue
                        }
                    }
                    // --- NACK Handling ---
                    else if (typeChar == '-')
                    {
                        ErrorLog.Notice("ViewsonicCDE: Command Rejected.");
                        lock (_queueLock) { ClearActiveCommand(); }
                    }
                    // --- Read Reply Handling ---
                    else if (typeChar == 'r' && msg.Length >= 8)
                    {
                        char cmdChar = msg[4];
                        string valString = msg.Substring(5, 3);

                        // Validation of TryParse
                        if (ushort.TryParse(valString, out ushort parsedVal))
                        {
                            if (cmdChar == '!')
                            {
                                if (valString == "001") safeInvoke = () => OnPowerChange?.Invoke(1);
                                else if (valString == "000") safeInvoke = () => OnPowerChange?.Invoke(0);
                            }
                            else if (cmdChar == 'f') safeInvoke = () => OnVolumeChange?.Invoke(parsedVal);
                            else if (cmdChar == '"') safeInvoke = () => OnInputChange?.Invoke(parsedVal);
                        }

                        lock (_queueLock) { ClearActiveCommand(); } // Read reply fulfills a Get request
                    }
                }

                // Invoke safely outside of all locks
                safeInvoke?.Invoke();

                // Trigger next item in queue
                ProcessQueue();

                crPos = _rxBuffer.IndexOf('\x0D');
            }
        }

        // =========================================================================
        // PUBLIC CONTROL METHODS
        // =========================================================================
        public void PowerOn() { EnqueueCommand("801s!001\x0D", 1, 1); }
        public void PollPower() { EnqueueCommand("501g!\x0D", 0, 0); }
        public void PollVolume() { EnqueueCommand("501gf\x0D", 0, 0); }
        public void PollInput() { EnqueueCommand("501g\"\x0D", 0, 0); }

        public void PowerOff()
        {
            EnqueueCommand("801s!000\x0D", 1, 0);
            EnqueueDelay(750);
            EnqueueCommand("801sA004\x0D", 0, 0);
        }

        public void SetInput(ushort inputVal)
        {
            EnqueueCommand(string.Format("801s\"{0:D3}\x0D", inputVal), 2, inputVal);
        }

        public void SetVolume(ushort volVal)
        {
            EnqueueCommand(string.Format("801sf{0:D3}\x0D", volVal), 3, volVal);
        }
    }
}