using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using V5RPC;
using V5RPC.Proto;

namespace V5DLLAdapter
{
    abstract class StrategyDllBase : IDisposable, IStrategy
    {
        protected IntPtr CurrentModule = IntPtr.Zero;
        public virtual bool IsLoaded => CurrentModule != IntPtr.Zero;

        protected bool ReverseCoordinate = false;

        [DllImport("kernel32.dll", SetLastError = true)]
        protected static extern IntPtr LoadLibrary(string lpLibFileName);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hLibModule);

        protected TDelegate LoadFunction<TDelegate>(string funcName) where TDelegate : Delegate
        {
            var ptr = GetProcAddress(CurrentModule, funcName);
            return (TDelegate)Marshal.GetDelegateForFunctionPointer(ptr, typeof(TDelegate));
        }

        public abstract string Dll { get; }

        // TODO: 是否应该直接抛错？
        public abstract bool Load(string dllPath, bool reverse, out Exception exception);
        public virtual void Unload()
        {
            if (IsLoaded)
            {
                FreeLibrary(CurrentModule);
            }
            CurrentModule = IntPtr.Zero;
        }

        public void Dispose()
        {
            Unload();
        }

        public abstract void OnEvent(EventType type, EventArguments arguments);
        public abstract TeamInfo GetTeamInfo(ServerInfo info);
        public abstract (Wheel[], ControlInfo) GetInstruction(Field field);
        public abstract ControlInfo GetControlInfo(ControlInfo controlInfo);
        public abstract Placement GetPlacement(Field field);
    }

    class StrategyDll : StrategyDllBase
    {
        #region UNMANAGED FUNCTIONS
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void GetTeamInfoDelegate(ref Native.TeamInfo teamInfo);
        GetTeamInfoDelegate _getTeamInfo;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void OnEventDelegate(EventType type, IntPtr arguments);
        OnEventDelegate _onEvent;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void GetInstructionDelegate(ref Native.Field field);
        GetInstructionDelegate _getInstruction;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void GetControlInfoDelegate(ref Native.ControlInfo controlInfo);
        GetControlInfoDelegate _getControlInfo;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void GetPlacementDelegate(ref Native.Field field);
        GetPlacementDelegate _getPlacement;
        #endregion

        string _lastDllPath = null;
        public override string Dll => IsLoaded ? _lastDllPath : null;

        public const int MAX_STRING_LEN = 128;

        public StrategyDll()
        {
        }

        public override bool Load(string dllPath, bool reverse, out Exception exception)
        {
            if (IsLoaded)
            {
                exception = new Exception("DLL has loaded.");
                return false;
            }

            ReverseCoordinate = reverse;
            _lastDllPath = dllPath;
            var hModule = LoadLibrary(dllPath);
            var errorCode = Marshal.GetLastWin32Error();
            if (hModule == IntPtr.Zero)
            {
                exception = new Exception($"Error load {dllPath}, code {errorCode}");
                return false;
            }
            CurrentModule = hModule;
            try
            {
                //BEGIN UNMANAGED FUNCTIONS
                _getTeamInfo = LoadFunction<GetTeamInfoDelegate>("GetTeamInfo");
                _onEvent = LoadFunction<OnEventDelegate>("OnEvent");
                _getInstruction = LoadFunction<GetInstructionDelegate>("GetInstruction");
                _getPlacement = LoadFunction<GetPlacementDelegate>("GetPlacement");
                _getControlInfo = LoadFunction<GetControlInfoDelegate>("GetControlInfo");
                //END UNMANAGED FUNCTIONS
            }
            catch (ArgumentNullException e)
            {
                Unload();
                exception = new Exception("Missing function", e);
                return false;
            }
            exception = null;
            return true;
        }

        public override void Unload()
        {
            base.Unload();
            //BEGIN UNMANAGED FUNCTIONS
            _getTeamInfo = null;
            _onEvent = null;
            _getInstruction = null;
            _getPlacement = null;
            //END UNMANAGED FUNCTIONS
        }

        [HandleProcessCorruptedStateExceptions]
        public override void OnEvent(EventType type, EventArguments arguments)
        {
            if (_getTeamInfo == null)
            {
                throw new DllNotFoundException();
            }
            switch (arguments.ArgumentCase)
            {
                case EventArguments.ArgumentOneofCase.JudgeResult:
                    {
                        var args = new Native.JudgeResultEvent
                        {
                            type = arguments.JudgeResult.Type,
                            offensiveTeam = arguments.JudgeResult.OffensiveTeam,
                            reason = arguments.JudgeResult.Reason
                        };
                        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(args));
                        Marshal.StructureToPtr(args, ptr, false);
                        try
                        {
                            _onEvent(type, ptr);
                        }
                        catch (Exception e)
                        {
                            throw new DllException("OnEvent", e);
                        }
                        finally
                        {
                            Marshal.DestroyStructure<Native.JudgeResultEvent>(ptr);
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                    return;
            }
            _onEvent(type, IntPtr.Zero);
        }

        [HandleProcessCorruptedStateExceptions]
        public override TeamInfo GetTeamInfo(ServerInfo info)
        {
            if (_getTeamInfo == null)
            {
                throw new DllNotFoundException();
            }
            var teamInfo = new Native.TeamInfo();
            try
            {
                _getTeamInfo(ref teamInfo);
            }
            catch (Exception e)
            {
                throw new DllException("GetTeamInfo", e);
            }
            return new TeamInfo
            {
                TeamName = teamInfo.teamName
            };
        }

        public override (Wheel[], ControlInfo) GetInstruction(Field field)
        {
            if (_getInstruction == null)
            {
                throw new DllNotFoundException();
            }

            // 将 Proto 的结构转为本地结构
            var nativeField = new Native.Field(field);
            var controlInfo = new ControlInfo();
            if (ReverseCoordinate)
            {
                // 翻转，获得黄方坐标
                nativeField.Reverse();
            }
            try
            {
                _getInstruction(ref nativeField);
            }
            catch (Exception e)
            {
                throw new DllException("GetInstruction", e);
            }
            return (nativeField.SelfRobots.Select(x => (Wheel) x.wheel).ToArray(),GetControlInfo(controlInfo));

        }
        public override ControlInfo GetControlInfo(ControlInfo controlInfo)
        {
            if (_getControlInfo == null)
            {
                throw new DllNotFoundException();
            }

            // 将 Proto 的结构转为本地结构
            var nativeControlInfo = new Native.ControlInfo(controlInfo);

            try
            {
                _getControlInfo(ref nativeControlInfo);
            }
            catch (Exception e)
            {
                throw new DllException("GetControl", e);
            }
            return (V5RPC.Proto.ControlInfo)nativeControlInfo;
        }
        public override Placement GetPlacement(Field field)
        {
            if (_getPlacement == null)
            {
                throw new DllNotFoundException();
            }
            var nativeField = new Native.Field(field);
            if (ReverseCoordinate)
            {
                nativeField.Reverse();
            }
            try
            {
                _getPlacement(ref nativeField);
            }
            catch (Exception e)
            {
                throw new DllException("GetPlacement", e);
            }
            return new Placement
            {
                Ball = (Ball)nativeField.ball,
                Robots =
                {
                    from x in nativeField.SelfRobots
                    select (Robot)(ReverseCoordinate ? x.Reverse() : x)
                }
            };
        }
    }

    /// <summary>
    /// 兼容老的 DLL
    /// </summary>
    class LegacyDll : StrategyDllBase
    {
        #region UNMANAGED FUNCTIONS
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void LegacyStrategyDelegate(ref Native.Legacy.Environment environment);

        LegacyStrategyDelegate _create;
        LegacyStrategyDelegate _strategy;
        LegacyStrategyDelegate _destroy;
        #endregion

        string _lastDllPath = null;
        public override string Dll => IsLoaded ? _lastDllPath : null;
        
        JudgeResultEvent.Types.ResultType gameState = JudgeResultEvent.Types.ResultType.PlaceKick;
        Team whosball = Team.Nobody;
        IntPtr userData = IntPtr.Zero;

        private readonly Placement placement = new Placement
        {
            Ball = new Ball {Position = new Vector2 {X = 0, Y = 0}},
            Robots =
            {
                new Robot {Position = new Vector2 {X = (float) 102.5, Y = 0}, Wheel = new Wheel()},
                new Robot {Position = new Vector2 {X = 81, Y = -48}, Wheel = new Wheel()},
                new Robot {Position = new Vector2 {X = 81, Y = 48}, Wheel = new Wheel()},
                new Robot {Position = new Vector2 {X = 30, Y = -48}, Wheel = new Wheel()},
                new Robot {Position = new Vector2 {X = 30, Y = 48}, Wheel = new Wheel()},
            }
        };

        public override bool Load(string dllPath, bool reverse, out Exception exception)
        {
            if (IsLoaded)
            {
                exception = new Exception("DLL has loaded.");
                return false;
            }
            
            ReverseCoordinate = reverse;
            _lastDllPath = dllPath;
            var hModule = LoadLibrary(dllPath);
            var errorCode = Marshal.GetLastWin32Error();
            if (hModule == IntPtr.Zero)
            {
                exception = new Exception($"Error load {dllPath}, code {errorCode}");
                return false;
            }
            CurrentModule = hModule;
            try
            {
                //BEGIN UNMANAGED FUNCTIONS
                _create = LoadFunction<LegacyStrategyDelegate>("Create");
                _strategy = LoadFunction<LegacyStrategyDelegate>("Strategy");
                _destroy = LoadFunction<LegacyStrategyDelegate>("Destroy");
                //END UNMANAGED FUNCTIONS
            }
            catch(ArgumentNullException e)
            {
                Unload();
                exception = new Exception("Missing function", e);
                return false;
            }

            var env = new Native.Legacy.Environment()
            {
                
                CurrentBall = new Native.Legacy.Ball {Position = new Native.Legacy.Vector3 {x = 50, y =  41.5}},
                SelfRobots =
                new [] {
                    new Native.Legacy.Robot {Position = new Native.Legacy.Vector3 {x = 90.5, y = 42}},
                    new Native.Legacy.Robot {Position = new Native.Legacy.Vector3 {x = 81, y = 23}},
                    new Native.Legacy.Robot {Position = new Native.Legacy.Vector3 {x = 81, y = 61}},
                    new Native.Legacy.Robot {Position = new Native.Legacy.Vector3 {x = 62, y = 23}},
                    new Native.Legacy.Robot {Position = new Native.Legacy.Vector3 {x = 62, y = 61}},
                }
            };
            
            _create?.Invoke(ref env);
            userData = env.UserData;
            exception = null;
            return true;
        }

        public override void Unload()
        {
            // 注释这两条代码之后可能导致内存泄漏，但会避免潜在的崩溃
            //var env = new Native.Legacy.Environment();
            //_destroy?.Invoke(ref env);

            base.Unload();

            _create = null;
            _strategy = null;
            _destroy = null;
        }

        public override (Wheel[], ControlInfo) GetInstruction(Field field)
        {
            if (_strategy == null)
            {
                throw new DllNotFoundException();
            }

            var nativeField = new Native.Field(field);
            var controlInfo = new ControlInfo();
            if (ReverseCoordinate)
            {
                nativeField.Reverse();
            }
            var env = new Native.Legacy.Environment(nativeField, whosball, gameState, userData);
            try
            {
                _strategy(ref env);
                userData = env.UserData;
            }
            catch (Exception e)
            {
                throw new DllException("Strategy", e);
            }

            return (env.SelfRobots.Select(x => new Wheel()
            {
                LeftSpeed = (float) x.VelocityLeft,
                RightSpeed = (float) x.VelocityRight
            }).ToArray(),
            GetControlInfo(controlInfo)
            );
        }

        public override Placement GetPlacement(Field field)
        {
            return placement;
        }

        public override ControlInfo GetControlInfo(ControlInfo controlInfo)
        {
            ControlInfo controlInfo_new = new ControlInfo();
            controlInfo_new.Command = ControlType.Continue;
            return controlInfo_new;
        }
        public override TeamInfo GetTeamInfo(ServerInfo info)
        {
            return new TeamInfo { TeamName = "Legacy DLL" };
        }

        public override void OnEvent(EventType type, EventArguments arguments)
        {
            switch (type)
            {
                case EventType.JudgeResult:
                    var judgeResult = arguments.JudgeResult;
                    gameState = judgeResult.Type;
                    whosball = judgeResult.OffensiveTeam;
                    break;
            }
        }
    }

    class EmptyDll : StrategyDllBase
    {
        private bool isLoaded = false;
        public override bool IsLoaded => isLoaded;

        public override string Dll => "Placeholder";

        public override (Wheel[], ControlInfo) GetInstruction(Field field)
        {
            var controlInfo = new ControlInfo();
            return (
            new Wheel[5]
            {
                new Wheel(),
                new Wheel(),
                new Wheel(),
                new Wheel(),
                new Wheel(),
            },
            GetControlInfo(controlInfo));
        }

        public override ControlInfo GetControlInfo(ControlInfo controlInfo)
        {
            ControlInfo controlInfo_new = new ControlInfo();
            controlInfo_new.Command = ControlType.Continue;
            return controlInfo_new;
        }

        public override Placement GetPlacement(Field field)
        {
            using (LegacyDll legacyDll = new LegacyDll())
                return legacyDll.GetPlacement(field);
        }

        public override TeamInfo GetTeamInfo(ServerInfo info)
        {
            return new TeamInfo() { TeamName = "Nobody" };
        }

        public override bool Load(string dllPath, bool reverse, out Exception exception)
        {
            isLoaded = true;
            exception = null;
            return true;
        }

        public override void OnEvent(EventType type, EventArguments arguments)
        {
            isLoaded = false;
            return;
        }
    }

    [Serializable]
    public class DllException : Exception
    {
        string _functionName;
        public override string Message { get { return $"在 DLL 导出的函数 {_functionName} 中发生异常"; } }
        public Exception MaskedInnerException { get; }

        public DllException(string functionName, Exception maskedInnerException)
        {
            _functionName = functionName;
            MaskedInnerException = maskedInnerException;
        }

        public DllException(string message) : base(message)
        {
        }

        public DllException()
        {
        }
    }

}
