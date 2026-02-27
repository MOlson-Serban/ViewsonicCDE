using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Linq;
using Crestron;
using Crestron.Logos.SplusLibrary;
using Crestron.Logos.SplusObjects;
using Crestron.SimplSharp;
using ViewsonicCDE;
using Crestron.SimplSharp.SimplSharpCloudClient;
using Crestron.SimplSharp.Python;

namespace UserModule_VIEWSONIC
{
    public class UserModuleClass_VIEWSONIC : SplusObject
    {
        static CCriticalSection g_criticalSection = new CCriticalSection();
        
        
        
        UShortParameter PORT;
        StringParameter IPADDRESS;
        Crestron.Logos.SplusObjects.DigitalInput CONNECT;
        Crestron.Logos.SplusObjects.DigitalInput POWER_ON;
        Crestron.Logos.SplusObjects.DigitalInput POWER_OFF;
        Crestron.Logos.SplusObjects.DigitalInput POLL_POWER;
        Crestron.Logos.SplusObjects.DigitalInput POLL_VOLUME;
        Crestron.Logos.SplusObjects.DigitalInput POLL_INPUT;
        Crestron.Logos.SplusObjects.DigitalInput VOLUME_UP;
        Crestron.Logos.SplusObjects.DigitalInput VOLUME_DOWN;
        InOutArray<Crestron.Logos.SplusObjects.DigitalInput> INPUT_POLL;
        Crestron.Logos.SplusObjects.DigitalOutput CONNECTED_FB;
        Crestron.Logos.SplusObjects.DigitalOutput IS_POWER_ON_FB;
        Crestron.Logos.SplusObjects.AnalogOutput CURRENT_INPUT_FB;
        Crestron.Logos.SplusObjects.AnalogOutput CURRENT_VOLUME_OUT;
        ViewsonicCDE.DisplayController MYDISPLAY;
        public void ONCONNECTIONCALLBACK ( ushort STATE ) 
            { 
            try
            {
                SplusExecutionContext __context__ = SplusSimplSharpDelegateThreadStartCode();
                
                __context__.SourceCodeLine = 51;
                CONNECTED_FB  .Value = (ushort) ( STATE ) ; 
                
                
            }
            finally { ObjectFinallyHandler(); }
            }
            
        public void ONPOWERCALLBACK ( ushort STATE ) 
            { 
            try
            {
                SplusExecutionContext __context__ = SplusSimplSharpDelegateThreadStartCode();
                
                __context__.SourceCodeLine = 52;
                IS_POWER_ON_FB  .Value = (ushort) ( STATE ) ; 
                
                
            }
            finally { ObjectFinallyHandler(); }
            }
            
        public void ONINPUTCALLBACK ( ushort VAL ) 
            { 
            try
            {
                SplusExecutionContext __context__ = SplusSimplSharpDelegateThreadStartCode();
                
                __context__.SourceCodeLine = 53;
                CURRENT_INPUT_FB  .Value = (ushort) ( VAL ) ; 
                
                
            }
            finally { ObjectFinallyHandler(); }
            }
            
        public void ONVOLUMECALLBACK ( ushort VAL ) 
            { 
            try
            {
                SplusExecutionContext __context__ = SplusSimplSharpDelegateThreadStartCode();
                
                __context__.SourceCodeLine = 54;
                CURRENT_VOLUME_OUT  .Value = (ushort) ( VAL ) ; 
                
                
            }
            finally { ObjectFinallyHandler(); }
            }
            
        object CONNECT_OnPush_0 ( Object __EventInfo__ )
        
            { 
            Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
            try
            {
                SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
                
                __context__.SourceCodeLine = 59;
                MYDISPLAY . Initialize ( IPADDRESS  .ToString(), (ushort)( PORT  .Value )) ; 
                
                
            }
            catch(Exception e) { ObjectCatchHandler(e); }
            finally { ObjectFinallyHandler( __SignalEventArg__ ); }
            return this;
            
        }
        
    object CONNECT_OnRelease_1 ( Object __EventInfo__ )
    
        { 
        Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
        try
        {
            SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
            
            __context__.SourceCodeLine = 64;
            MYDISPLAY . Disconnect ( ) ; 
            
            
        }
        catch(Exception e) { ObjectCatchHandler(e); }
        finally { ObjectFinallyHandler( __SignalEventArg__ ); }
        return this;
        
    }
    
object POWER_ON_OnPush_2 ( Object __EventInfo__ )

    { 
    Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
    try
    {
        SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
        
        __context__.SourceCodeLine = 67;
        MYDISPLAY . PowerOn ( ) ; 
        
        
    }
    catch(Exception e) { ObjectCatchHandler(e); }
    finally { ObjectFinallyHandler( __SignalEventArg__ ); }
    return this;
    
}

object POWER_OFF_OnPush_3 ( Object __EventInfo__ )

    { 
    Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
    try
    {
        SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
        
        __context__.SourceCodeLine = 68;
        MYDISPLAY . PowerOff ( ) ; 
        
        
    }
    catch(Exception e) { ObjectCatchHandler(e); }
    finally { ObjectFinallyHandler( __SignalEventArg__ ); }
    return this;
    
}

object POLL_POWER_OnPush_4 ( Object __EventInfo__ )

    { 
    Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
    try
    {
        SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
        
        __context__.SourceCodeLine = 69;
        MYDISPLAY . PollPower ( ) ; 
        
        
    }
    catch(Exception e) { ObjectCatchHandler(e); }
    finally { ObjectFinallyHandler( __SignalEventArg__ ); }
    return this;
    
}

object POLL_VOLUME_OnPush_5 ( Object __EventInfo__ )

    { 
    Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
    try
    {
        SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
        
        __context__.SourceCodeLine = 70;
        MYDISPLAY . PollVolume ( ) ; 
        
        
    }
    catch(Exception e) { ObjectCatchHandler(e); }
    finally { ObjectFinallyHandler( __SignalEventArg__ ); }
    return this;
    
}

object POLL_INPUT_OnPush_6 ( Object __EventInfo__ )

    { 
    Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
    try
    {
        SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
        
        __context__.SourceCodeLine = 71;
        MYDISPLAY . PollInput ( ) ; 
        
        
    }
    catch(Exception e) { ObjectCatchHandler(e); }
    finally { ObjectFinallyHandler( __SignalEventArg__ ); }
    return this;
    
}

object INPUT_POLL_OnPush_7 ( Object __EventInfo__ )

    { 
    Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
    try
    {
        SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
        ushort I = 0;
        
        ushort VIEWSONICINPUTVAL = 0;
        
        
        __context__.SourceCodeLine = 78;
        I = (ushort) ( Functions.GetLastModifiedArrayIndex( __SignalEventArg__ ) ) ; 
        __context__.SourceCodeLine = 81;
        
            {
            int __SPLS_TMPVAR__SWTCH_1__ = ((int)I);
            
                { 
                if  ( Functions.TestForTrue  (  ( __SPLS_TMPVAR__SWTCH_1__ == ( 1) ) ) ) 
                    { 
                    __context__.SourceCodeLine = 83;
                    VIEWSONICINPUTVAL = (ushort) ( 4 ) ; 
                    } 
                
                else if  ( Functions.TestForTrue  (  ( __SPLS_TMPVAR__SWTCH_1__ == ( 2) ) ) ) 
                    { 
                    __context__.SourceCodeLine = 84;
                    VIEWSONICINPUTVAL = (ushort) ( 14 ) ; 
                    } 
                
                else if  ( Functions.TestForTrue  (  ( __SPLS_TMPVAR__SWTCH_1__ == ( 3) ) ) ) 
                    { 
                    __context__.SourceCodeLine = 85;
                    VIEWSONICINPUTVAL = (ushort) ( 9 ) ; 
                    } 
                
                else if  ( Functions.TestForTrue  (  ( __SPLS_TMPVAR__SWTCH_1__ == ( 4) ) ) ) 
                    { 
                    __context__.SourceCodeLine = 86;
                    VIEWSONICINPUTVAL = (ushort) ( 6 ) ; 
                    } 
                
                else 
                    { 
                    __context__.SourceCodeLine = 90;
                    VIEWSONICINPUTVAL = (ushort) ( 0 ) ; 
                    __context__.SourceCodeLine = 91;
                    Trace( "Viewsonic Error: Invalid Input Index {0:d} received.", (short)I) ; 
                    } 
                
                } 
                
            }
            
        
        __context__.SourceCodeLine = 95;
        if ( Functions.TestForTrue  ( ( Functions.BoolToInt ( VIEWSONICINPUTVAL > 0 ))  ) ) 
            { 
            __context__.SourceCodeLine = 97;
            MYDISPLAY . SetInput ( (ushort)( VIEWSONICINPUTVAL )) ; 
            } 
        
        
        
    }
    catch(Exception e) { ObjectCatchHandler(e); }
    finally { ObjectFinallyHandler( __SignalEventArg__ ); }
    return this;
    
}

object VOLUME_UP_OnPush_8 ( Object __EventInfo__ )

    { 
    Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
    try
    {
        SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
        
        __context__.SourceCodeLine = 104;
        if ( Functions.TestForTrue  ( ( Functions.BoolToInt ( CURRENT_VOLUME_OUT  .Value < 100 ))  ) ) 
            { 
            __context__.SourceCodeLine = 106;
            MYDISPLAY . SetVolume ( (ushort)( (CURRENT_VOLUME_OUT  .Value + 1) )) ; 
            } 
        
        
        
    }
    catch(Exception e) { ObjectCatchHandler(e); }
    finally { ObjectFinallyHandler( __SignalEventArg__ ); }
    return this;
    
}

object VOLUME_DOWN_OnPush_9 ( Object __EventInfo__ )

    { 
    Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
    try
    {
        SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
        
        __context__.SourceCodeLine = 112;
        if ( Functions.TestForTrue  ( ( Functions.BoolToInt ( CURRENT_VOLUME_OUT  .Value > 0 ))  ) ) 
            { 
            __context__.SourceCodeLine = 114;
            MYDISPLAY . SetVolume ( (ushort)( (CURRENT_VOLUME_OUT  .Value - 1) )) ; 
            } 
        
        
        
    }
    catch(Exception e) { ObjectCatchHandler(e); }
    finally { ObjectFinallyHandler( __SignalEventArg__ ); }
    return this;
    
}

public override object FunctionMain (  object __obj__ ) 
    { 
    try
    {
        SplusExecutionContext __context__ = SplusFunctionMainStartCode();
        
        __context__.SourceCodeLine = 121;
        WaitForInitializationComplete ( ) ; 
        __context__.SourceCodeLine = 123;
        // RegisterDelegate( MYDISPLAY , ONCONNECTIONCHANGE , ONCONNECTIONCALLBACK ) 
        MYDISPLAY .OnConnectionChange  = ONCONNECTIONCALLBACK; ; 
        __context__.SourceCodeLine = 124;
        // RegisterDelegate( MYDISPLAY , ONPOWERCHANGE , ONPOWERCALLBACK ) 
        MYDISPLAY .OnPowerChange  = ONPOWERCALLBACK; ; 
        __context__.SourceCodeLine = 125;
        // RegisterDelegate( MYDISPLAY , ONINPUTCHANGE , ONINPUTCALLBACK ) 
        MYDISPLAY .OnInputChange  = ONINPUTCALLBACK; ; 
        __context__.SourceCodeLine = 126;
        // RegisterDelegate( MYDISPLAY , ONVOLUMECHANGE , ONVOLUMECALLBACK ) 
        MYDISPLAY .OnVolumeChange  = ONVOLUMECALLBACK; ; 
        
        
    }
    catch(Exception e) { ObjectCatchHandler(e); }
    finally { ObjectFinallyHandler(); }
    return __obj__;
    }
    

public override void LogosSplusInitialize()
{
    SocketInfo __socketinfo__ = new SocketInfo( 1, this );
    InitialParametersClass.ResolveHostName = __socketinfo__.ResolveHostName;
    _SplusNVRAM = new SplusNVRAM( this );
    
    CONNECT = new Crestron.Logos.SplusObjects.DigitalInput( CONNECT__DigitalInput__, this );
    m_DigitalInputList.Add( CONNECT__DigitalInput__, CONNECT );
    
    POWER_ON = new Crestron.Logos.SplusObjects.DigitalInput( POWER_ON__DigitalInput__, this );
    m_DigitalInputList.Add( POWER_ON__DigitalInput__, POWER_ON );
    
    POWER_OFF = new Crestron.Logos.SplusObjects.DigitalInput( POWER_OFF__DigitalInput__, this );
    m_DigitalInputList.Add( POWER_OFF__DigitalInput__, POWER_OFF );
    
    POLL_POWER = new Crestron.Logos.SplusObjects.DigitalInput( POLL_POWER__DigitalInput__, this );
    m_DigitalInputList.Add( POLL_POWER__DigitalInput__, POLL_POWER );
    
    POLL_VOLUME = new Crestron.Logos.SplusObjects.DigitalInput( POLL_VOLUME__DigitalInput__, this );
    m_DigitalInputList.Add( POLL_VOLUME__DigitalInput__, POLL_VOLUME );
    
    POLL_INPUT = new Crestron.Logos.SplusObjects.DigitalInput( POLL_INPUT__DigitalInput__, this );
    m_DigitalInputList.Add( POLL_INPUT__DigitalInput__, POLL_INPUT );
    
    VOLUME_UP = new Crestron.Logos.SplusObjects.DigitalInput( VOLUME_UP__DigitalInput__, this );
    m_DigitalInputList.Add( VOLUME_UP__DigitalInput__, VOLUME_UP );
    
    VOLUME_DOWN = new Crestron.Logos.SplusObjects.DigitalInput( VOLUME_DOWN__DigitalInput__, this );
    m_DigitalInputList.Add( VOLUME_DOWN__DigitalInput__, VOLUME_DOWN );
    
    INPUT_POLL = new InOutArray<DigitalInput>( 4, this );
    for( uint i = 0; i < 4; i++ )
    {
        INPUT_POLL[i+1] = new Crestron.Logos.SplusObjects.DigitalInput( INPUT_POLL__DigitalInput__ + i, INPUT_POLL__DigitalInput__, this );
        m_DigitalInputList.Add( INPUT_POLL__DigitalInput__ + i, INPUT_POLL[i+1] );
    }
    
    CONNECTED_FB = new Crestron.Logos.SplusObjects.DigitalOutput( CONNECTED_FB__DigitalOutput__, this );
    m_DigitalOutputList.Add( CONNECTED_FB__DigitalOutput__, CONNECTED_FB );
    
    IS_POWER_ON_FB = new Crestron.Logos.SplusObjects.DigitalOutput( IS_POWER_ON_FB__DigitalOutput__, this );
    m_DigitalOutputList.Add( IS_POWER_ON_FB__DigitalOutput__, IS_POWER_ON_FB );
    
    CURRENT_INPUT_FB = new Crestron.Logos.SplusObjects.AnalogOutput( CURRENT_INPUT_FB__AnalogSerialOutput__, this );
    m_AnalogOutputList.Add( CURRENT_INPUT_FB__AnalogSerialOutput__, CURRENT_INPUT_FB );
    
    CURRENT_VOLUME_OUT = new Crestron.Logos.SplusObjects.AnalogOutput( CURRENT_VOLUME_OUT__AnalogSerialOutput__, this );
    m_AnalogOutputList.Add( CURRENT_VOLUME_OUT__AnalogSerialOutput__, CURRENT_VOLUME_OUT );
    
    PORT = new UShortParameter( PORT__Parameter__, this );
    m_ParameterList.Add( PORT__Parameter__, PORT );
    
    IPADDRESS = new StringParameter( IPADDRESS__Parameter__, this );
    m_ParameterList.Add( IPADDRESS__Parameter__, IPADDRESS );
    
    
    CONNECT.OnDigitalPush.Add( new InputChangeHandlerWrapper( CONNECT_OnPush_0, false ) );
    CONNECT.OnDigitalRelease.Add( new InputChangeHandlerWrapper( CONNECT_OnRelease_1, false ) );
    POWER_ON.OnDigitalPush.Add( new InputChangeHandlerWrapper( POWER_ON_OnPush_2, false ) );
    POWER_OFF.OnDigitalPush.Add( new InputChangeHandlerWrapper( POWER_OFF_OnPush_3, false ) );
    POLL_POWER.OnDigitalPush.Add( new InputChangeHandlerWrapper( POLL_POWER_OnPush_4, false ) );
    POLL_VOLUME.OnDigitalPush.Add( new InputChangeHandlerWrapper( POLL_VOLUME_OnPush_5, false ) );
    POLL_INPUT.OnDigitalPush.Add( new InputChangeHandlerWrapper( POLL_INPUT_OnPush_6, false ) );
    for( uint i = 0; i < 4; i++ )
        INPUT_POLL[i+1].OnDigitalPush.Add( new InputChangeHandlerWrapper( INPUT_POLL_OnPush_7, false ) );
        
    VOLUME_UP.OnDigitalPush.Add( new InputChangeHandlerWrapper( VOLUME_UP_OnPush_8, false ) );
    VOLUME_DOWN.OnDigitalPush.Add( new InputChangeHandlerWrapper( VOLUME_DOWN_OnPush_9, false ) );
    
    _SplusNVRAM.PopulateCustomAttributeList( true );
    
    NVRAM = _SplusNVRAM;
    
}

public override void LogosSimplSharpInitialize()
{
    MYDISPLAY  = new ViewsonicCDE.DisplayController();
    
    
}

public UserModuleClass_VIEWSONIC ( string InstanceName, string ReferenceID, Crestron.Logos.SplusObjects.CrestronStringEncoding nEncodingType ) : base( InstanceName, ReferenceID, nEncodingType ) {}




const uint PORT__Parameter__ = 10;
const uint IPADDRESS__Parameter__ = 11;
const uint CONNECT__DigitalInput__ = 0;
const uint POWER_ON__DigitalInput__ = 1;
const uint POWER_OFF__DigitalInput__ = 2;
const uint POLL_POWER__DigitalInput__ = 3;
const uint POLL_VOLUME__DigitalInput__ = 4;
const uint POLL_INPUT__DigitalInput__ = 5;
const uint VOLUME_UP__DigitalInput__ = 6;
const uint VOLUME_DOWN__DigitalInput__ = 7;
const uint INPUT_POLL__DigitalInput__ = 8;
const uint CONNECTED_FB__DigitalOutput__ = 0;
const uint IS_POWER_ON_FB__DigitalOutput__ = 1;
const uint CURRENT_INPUT_FB__AnalogSerialOutput__ = 0;
const uint CURRENT_VOLUME_OUT__AnalogSerialOutput__ = 1;

[SplusStructAttribute(-1, true, false)]
public class SplusNVRAM : SplusStructureBase
{

    public SplusNVRAM( SplusObject __caller__ ) : base( __caller__ ) {}
    
    
}

SplusNVRAM _SplusNVRAM = null;

public class __CEvent__ : CEvent
{
    public __CEvent__() {}
    public void Close() { base.Close(); }
    public int Reset() { return base.Reset() ? 1 : 0; }
    public int Set() { return base.Set() ? 1 : 0; }
    public int Wait( int timeOutInMs ) { return base.Wait( timeOutInMs ) ? 1 : 0; }
}
public class __CMutex__ : CMutex
{
    public __CMutex__() {}
    public void Close() { base.Close(); }
    public void ReleaseMutex() { base.ReleaseMutex(); }
    public int WaitForMutex() { return base.WaitForMutex() ? 1 : 0; }
}
 public int IsNull( object obj ){ return (obj == null) ? 1 : 0; }
}


}
