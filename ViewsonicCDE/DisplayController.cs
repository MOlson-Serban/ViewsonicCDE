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

        // Protocol / input limits
        private const int MAX_QUEUE_DEPTH = 50;   // Bounds the command queue (fix #5)
        private const ushort MAX_VOLUME = 100;    // Panel volume range 0-100
        private const ushort MAX_INPUT_CODE = 999; // Fixed-width 3-digit input code

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
        private enum CmdType
        {
            RawOrGet = 0,
            Power = 1,
            Input = 2,
            Volume = 3
        }

        private class QueuedCmd
        {
            public string CommandString;
            public CmdType Type;
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
                    StopTimer(ref _heartbeatTimer); // Fix #1: dispose any prior heartbeat before re-creating
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

            // Fix #3: guard the async connect so a throw never escapes the timer thread.
            try
            {
                if (_enableConnection && _tcpClient != null && _tcpClient.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    _tcpClient.ConnectToServerAsync(ConnectCallback);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("ViewsonicCDE Reconnect Error: " + ex.Message);

                // Reschedule another attempt so we don't get stuck without a pending reconnect.
                if (_enableConnection)
                {
                    lock (_timerLock)
                    {
                        if (_reconnectTimer == null)
                            _reconnectTimer = new CTimer(ReconnectCallback, null, _currentBackoffMs);
                    }
                }
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
        private void EnqueueCommand(string command, CmdType type, ushort val)
        {
            lock (_queueLock)
            {
                // Fix #5: bound the queue so an unresponsive panel can't grow it without limit.
                if (_cmdQueue.Count >= MAX_QUEUE_DEPTH)
                {
                    ErrorLog.Notice("ViewsonicCDE: Command queue full, dropping command.");
                    return;
                }
                _cmdQueue.Enqueue(new QueuedCmd { CommandString = command, Type = type, PendingValue = val, IsDelay = false });
            }
            ProcessQueue();
        }

        private void EnqueueDelay(int delayMs)
        {
            lock (_queueLock)
            {
                if (_cmdQueue.Count >= MAX_QUEUE_DEPTH)
                {
                    ErrorLog.Notice("ViewsonicCDE: Command queue full, dropping delay.");
                    return;
                }
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

        private void SendCallback(TCPClient client, int numberOfBytesSent)
        {
            // Fix #8: surface short/failed sends instead of silently waiting for a timeout.
            if (numberOfBytesSent <= 0)
                ErrorLog.Notice("ViewsonicCDE: Send reported 0 bytes (socket may be closing).");
        }

        // =========================================================================
        // RECEPTION & PARSING
        // =========================================================================
        private void ReceiveCallback(TCPClient client, int numberOfBytesReceived)
        {
            try
            {
                // Fix #4: a non-positive count signals a closed/aborted read; let
                // OnSocketStatusChange handle the disconnect and don't re-arm here.
                if (numberOfBytesReceived <= 0)
                    return;

                string incoming = Encoding.ASCII.GetString(client.IncomingDataBuffer, 0, numberOfBytesReceived);

                // Fix #6: extract complete frames under the buffer lock, then dispatch
                // (invoke delegates + advance the queue) outside the lock.
                List<string> messages = new List<string>();
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

                    ExtractMessages(messages);
                }

                foreach (string msg in messages)
                    HandleMessage(msg);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("ViewsonicCDE Receive Error: " + ex.Message);
            }
            finally
            {
                // Fix #4: always re-arm reception while the socket is up, regardless of
                // how the body above exited.
                if (client != null && client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                    client.ReceiveDataAsync(ReceiveCallback);
            }
        }

        // Caller must hold _bufferLock.
        private void ExtractMessages(List<string> messages)
        {
            int crPos = _rxBuffer.IndexOf('\x0D');
            while (crPos >= 0)
            {
                messages.Add(_rxBuffer.Substring(0, crPos));
                _rxBuffer = _rxBuffer.Substring(crPos + 1);
                crPos = _rxBuffer.IndexOf('\x0D');
            }
        }

        private void HandleMessage(string msg)
        {
            // Variable for safe delegate invocation
            Action safeInvoke = null;

            if (msg.Length >= 4) // Validation
            {
                char typeChar = msg[3];

                // --- ACK Handling ---
                if (typeChar == '+')
                {
                    lock (_queueLock)
                    {
                        if (_activeCmd != null && _activeCmd.Type != CmdType.RawOrGet)
                        {
                            ushort val = _activeCmd.PendingValue;
                            if (_activeCmd.Type == CmdType.Power) safeInvoke = () => OnPowerChange?.Invoke(val);
                            else if (_activeCmd.Type == CmdType.Input) safeInvoke = () => OnInputChange?.Invoke(val);
                            else if (_activeCmd.Type == CmdType.Volume) safeInvoke = () => OnVolumeChange?.Invoke(val);
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
        }

        // =========================================================================
        // PUBLIC CONTROL METHODS
        // =========================================================================
        public void PowerOn() { EnqueueCommand("801s!001\x0D", CmdType.Power, 1); }
        public void PollPower() { EnqueueCommand("501g!\x0D", CmdType.RawOrGet, 0); }
        public void PollVolume() { EnqueueCommand("501gf\x0D", CmdType.RawOrGet, 0); }
        public void PollInput() { EnqueueCommand("501g\"\x0D", CmdType.RawOrGet, 0); }

        public void PowerOff()
        {
            EnqueueCommand("801s!000\x0D", CmdType.Power, 0);
            EnqueueDelay(750);
            EnqueueCommand("801sA004\x0D", CmdType.RawOrGet, 0);
        }

        public void SetInput(ushort inputVal)
        {
            // Fix #2: clamp to the 3-digit field so a large value can't break frame width.
            if (inputVal > MAX_INPUT_CODE)
            {
                ErrorLog.Notice(string.Format("ViewsonicCDE: Input {0} out of range, clamping to {1}.", inputVal, MAX_INPUT_CODE));
                inputVal = MAX_INPUT_CODE;
            }
            EnqueueCommand(string.Format("801s\"{0:D3}\x0D", inputVal), CmdType.Input, inputVal);
        }

        public void SetVolume(ushort volVal)
        {
            // Fix #2: clamp to the panel's volume range and keep the field 3 digits wide.
            if (volVal > MAX_VOLUME)
            {
                ErrorLog.Notice(string.Format("ViewsonicCDE: Volume {0} out of range, clamping to {1}.", volVal, MAX_VOLUME));
                volVal = MAX_VOLUME;
            }
            EnqueueCommand(string.Format("801sf{0:D3}\x0D", volVal), CmdType.Volume, volVal);
        }
    }
}
