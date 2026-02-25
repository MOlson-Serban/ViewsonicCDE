using System;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace ViewsonicCDE
{
    public class DisplayController
    {
        private TCPClient _tcpClient;
        private CTimer _heartbeatTimer;
        private CTimer _reconnectTimer;
        private CTimer _powerOffEnterTimer;
        private string _rxBuffer = "";

        private readonly object _bufferLock = new object();
        private readonly object _stateLock = new object();
        private readonly object _timerLock = new object();

        private bool _enableConnection = false;

        private int _pendingCmdType = 0;
        private int _pendingPower = 0;
        private int _pendingInput = 0;
        private int _pendingVolume = 0;

        public delegate void StateChangeHandler(ushort state);
        public StateChangeHandler OnConnectionChange { get; set; }
        public StateChangeHandler OnPowerChange { get; set; }
        public StateChangeHandler OnInputChange { get; set; }
        public StateChangeHandler OnVolumeChange { get; set; }

        public void Initialize(string ipAddress, ushort port)
        {
            try
            {
                _enableConnection = true;

                if (_tcpClient != null && _tcpClient.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                    _tcpClient.DisconnectFromServer();

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
            _enableConnection = false; // Prevent auto-reconnect
            StopTimers();

            if (_tcpClient != null)
                _tcpClient.DisconnectFromServer();
        }

        private void ConnectCallback(TCPClient client)
        {
            if (client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
            {
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
                if (OnConnectionChange != null) OnConnectionChange(1);

                lock (_timerLock)
                {
                    if (_reconnectTimer != null)
                    {
                        _reconnectTimer.Stop();
                        _reconnectTimer.Dispose();
                        _reconnectTimer = null;
                    }
                    _heartbeatTimer = new CTimer(HeartbeatCallback, null, 45000, 45000);
                }
            }
            else
            {
                if (OnConnectionChange != null) OnConnectionChange(0);
                StopTimers();

                // Auto-Reconnect Logic
                if (_enableConnection)
                {
                    lock (_timerLock)
                    {
                        _reconnectTimer = new CTimer(ReconnectCallback, null, 5000);
                    }
                }
            }
        }

        private void ReconnectCallback(object userSpecific)
        {
            if (_enableConnection && _tcpClient != null && _tcpClient.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                _tcpClient.ConnectToServerAsync(ConnectCallback);
            }
        }

        private void StopTimers()
        {
            lock (_timerLock)
            {
                if (_heartbeatTimer != null)
                {
                    _heartbeatTimer.Stop();
                    _heartbeatTimer.Dispose();
                    _heartbeatTimer = null;
                }

                // Added disposal for the reconnect timer
                if (_reconnectTimer != null)
                {
                    _reconnectTimer.Stop();
                    _reconnectTimer.Dispose();
                    _reconnectTimer = null;
                }
            }
        }

        private void HeartbeatCallback(object userSpecific)
        {
            PollPower();
        }

        private void SendCommand(string command)
        {
            try
            {
                if (_tcpClient != null && _tcpClient.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(command);
                    _tcpClient.SendDataAsync(bytes, bytes.Length, SendCallback);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("ViewsonicCDE Send Error: " + ex.Message);
            }
        }

        private void SendCallback(TCPClient client, int numberOfBytesSent) { }

        private void ReceiveCallback(TCPClient client, int numberOfBytesReceived)
        {
            if (numberOfBytesReceived > 0)
            {
                string incoming = Encoding.ASCII.GetString(client.IncomingDataBuffer, 0, numberOfBytesReceived);

                lock (_bufferLock)
                {
                    _rxBuffer += incoming;

                    // Non-destructive overflow protection
                    if (_rxBuffer.Length > 512)
                    {
                        _rxBuffer = _rxBuffer.Substring(256);
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

                if (msg.Length >= 4)
                {
                    char typeChar = msg[3];

                    if (typeChar == '+')
                    {
                        lock (_stateLock)
                        {
                            if (_pendingCmdType == 1 && OnPowerChange != null) OnPowerChange((ushort)_pendingPower);
                            else if (_pendingCmdType == 2 && OnInputChange != null) OnInputChange((ushort)_pendingInput);
                            else if (_pendingCmdType == 3 && OnVolumeChange != null) OnVolumeChange((ushort)_pendingVolume);
                            _pendingCmdType = 0;
                        }
                    }
                    else if (typeChar == '-')
                    {
                        lock (_stateLock) { _pendingCmdType = 0; }
                    }
                    else if (typeChar == 'r' && msg.Length >= 8)
                    {
                        char cmdChar = msg[4];
                        string valString = msg.Substring(5, 3);
                        ushort parsedVal;
                        ushort.TryParse(valString, out parsedVal);

                        if (cmdChar == '!')
                        {
                            if (valString == "001" && OnPowerChange != null) OnPowerChange(1);
                            else if (valString == "000" && OnPowerChange != null) OnPowerChange(0);
                        }
                        else if (cmdChar == 'f' && OnVolumeChange != null)
                        {
                            OnVolumeChange(parsedVal);
                        }
                        else if (cmdChar == '"' && OnInputChange != null)
                        {
                            OnInputChange(parsedVal);
                        }
                    }
                }
                crPos = _rxBuffer.IndexOf('\x0D');
            }
        }

        public void PowerOn() { lock (_stateLock) { _pendingCmdType = 1; _pendingPower = 1; } SendCommand("801s!001\x0D"); }

        public void PowerOff()
        {
            lock (_stateLock)
            {
                _pendingCmdType = 1;
                _pendingPower = 0;
            }

            // 1. Send the initial Power Off command
            SendCommand("801s!000\x0D");

            // 2. Start a 2500ms timer to send the Enter key confirmation. 
            // Omitting the 4th parameter defaults it to a one-shot timer.
            _powerOffEnterTimer = new CTimer(SendPowerOffEnterCallback, null, 700);
        }

        private void SendPowerOffEnterCallback(object userSpecific)
        {
            // Send the Key Pad 'Enter' command (Command 'A', Value '004')
            SendCommand("801sA004\x0D");

            // Notice: We do NOT call _powerOffEnterTimer.Dispose() here to prevent thread crashing.
        }

        public void PollPower() { SendCommand("501g!\x0D"); }
        public void PollVolume() { SendCommand("501gf\x0D"); }
        public void PollInput() { SendCommand("501g\"\x0D"); }

        public void SetInput(ushort inputVal)
        {
            lock (_stateLock)
            {
                _pendingCmdType = 2;
                _pendingInput = inputVal;
            }
            SendCommand(string.Format("801s\"{0:D3}\x0D", inputVal));
        }

        public void SetVolume(ushort volVal)
        {
            lock (_stateLock)
            {
                _pendingCmdType = 3;
                _pendingVolume = volVal;
            }
            SendCommand(string.Format("801sf{0:D3}\x0D", volVal));
        }
    }
}