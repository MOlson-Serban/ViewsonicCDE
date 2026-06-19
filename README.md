# ViewSonic CDE Display Driver (Crestron)

A Crestron control module for ViewSonic CDE-series commercial displays over a
TCP/IP network connection. It is built as a SIMPL# (C#) library that does all the
heavy lifting, paired with a thin SIMPL+ wrapper that exposes the functionality as
standard Crestron signals for use inside a SIMPL Windows program.

## Architecture

The driver is split into two cooperating pieces:

| File | Role |
|------|------|
| `ViewsonicCDE/DisplayController.cs` | SIMPL# library — owns the TCP socket, command queue, reconnect logic, and protocol parsing. Surfaces a small C# API and state-change callbacks. |
| `Viewsonic.usp` | SIMPL+ wrapper — maps Crestron digital/analog signals to the library's methods and routes the library's callbacks back out to feedback signals. |

The SIMPL+ wrapper references the compiled library via
`#USER_SIMPLSHARP_LIBRARY "ViewsonicCDE"` and holds a single `DisplayController`
instance. All device communication, threading, and timing live in the library;
the wrapper contains only signal plumbing and the input-code mapping.

## The library — `DisplayController`

The library manages a single display connection and presents an event-driven API.

### Public methods

| Method | Description |
|--------|-------------|
| `Initialize(string ip, ushort port)` | Open (or re-open) the connection and begin auto-reconnect/heartbeat management. |
| `Disconnect()` | Tear down the connection, stop all timers, and clear the command queue. |
| `PowerOn()` / `PowerOff()` | Power control. `PowerOff` issues a short follow-up sequence after a brief delay. |
| `SetInput(ushort code)` | Select an input by its ViewSonic protocol code. |
| `SetVolume(ushort level)` | Set volume (clamped to 0–100). |
| `PollPower()` / `PollInput()` / `PollVolume()` | Request the current state of each property. |

### Callbacks

Consumers assign handlers to be notified of state changes:

`OnConnectionChange`, `OnPowerChange`, `OnInputChange`, `OnVolumeChange`
— each is a `StateChangeHandler(ushort state)`.

### Behavior and resilience

- **Asynchronous TCP** via Crestron `TCPClient`; no blocking calls on the program thread.
- **Serialized command queue** — commands are sent one at a time and the queue
  advances on ACK/NACK or a per-command timeout, so the display is never flooded.
  Inter-command delays are supported (used by the power-off sequence).
- **Automatic reconnect** with exponential backoff (5 s up to 60 s), reset on a
  successful connect.
- **Heartbeat polling** every 45 s re-reads power, input, and volume to keep
  feedback in sync and detect a silently dropped link.
- **Framed parsing** — incoming bytes are buffered and split on carriage return
  (`0x0D`), with overflow protection that trims to the last complete message.
- **Defensive limits** — volume and input values are clamped to their valid field
  width, and the command queue is bounded to prevent unbounded growth against an
  unresponsive panel.

### Key constants

| Constant | Value |
|----------|-------|
| Heartbeat interval | 45 s |
| Reconnect backoff | 5 s → 60 s |
| Command timeout | 3 s |
| Max RX buffer | 1024 bytes |
| Max queue depth | 50 |
| Volume range | 0–100 |

## The SIMPL+ wrapper — `Viewsonic.usp`

### Parameters

| Parameter | Type | Notes |
|-----------|------|-------|
| `IPAddress` | String[32] | Display IP / hostname |
| `Port` | Integer | TCP port (0–65535) |

### Inputs (digital)

| Signal | Action |
|--------|--------|
| `Connect` | **Maintained high.** Rising edge connects; dropping it low disconnects. |
| `Power_On` / `Power_Off` | Power control |
| `Volume_Up` / `Volume_Down` | Step volume by one (driven from an internal shadow value for reliable ramping) |
| `Poll_Power` / `Poll_Volume` / `Poll_Input` | Manually request current state |
| `Input_Select[1..4]` | Select an input by logical index (see mapping below) |

### Outputs

| Signal | Type | Meaning |
|--------|------|---------|
| `Connected_fb` | Digital | High while the socket is connected |
| `Is_Power_On_fb` | Digital | Current power state |
| `Current_Input_fb` | Analog | Currently selected input as a logical index (1–4) |
| `Current_Volume_Out` | Analog | Current volume (0–100) |

### Input mapping

`Input_Select` uses logical indices that the wrapper translates to ViewSonic
protocol codes. The same mapping is applied in reverse for `Current_Input_fb`, so
the selection and feedback sides share one numbering scheme.

| Index | Input | Protocol code |
|-------|-------|---------------|
| 1 | HDMI 1 | 004 |
| 2 | HDMI 2 | 014 |
| 3 | DisplayPort | 009 |
| 4 | VGA | 006 |

## Protocol notes

Commands are ASCII strings terminated with a carriage return (`0x0D`). Replies are
classified by their fourth character: `+` (ACK), `-` (NACK/rejected), and `r`
(read reply, carrying a value). The library parses read replies for power, input,
and volume and raises the corresponding callback.

## Building and deploying

1. Open `ViewsonicCDE/` in Visual Studio with the Crestron SimplSharp SDK
   (targets `net47`) and build the library; this produces the loadable module.
2. Compile `Viewsonic.usp` in SIMPL+ Cross Compiler / SIMPL Windows. It links the
   library through `#USER_SIMPLSHARP_LIBRARY "ViewsonicCDE"`.
3. Drop the resulting module into your SIMPL Windows program, wire the signals,
   and set the `IPAddress` and `Port` parameters.

> Note: the SIMPL+ compiler regenerates `SPlsWork/` (including `Viewsonic.cs`) on
> every build — these are generated artifacts and should not be edited by hand.
