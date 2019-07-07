using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using V5RPC;
using V5RPC.Proto;

namespace V5DLLAdapter
{
    abstract class StrategyDllBase : IDisposable, IStrategy
    {
        protected IntPtr currentModule = IntPtr.Zero;
        public bool IsLoaded => currentModule != IntPtr.Zero;

        [DllImport("kernel32.dll")]
        static protected extern IntPtr LoadLibrary(string lpLibFileName);
        [DllImport("kernel32.dll")]
        static protected extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        [DllImport("kernel32.dll")]
        static protected extern bool FreeLibrary(IntPtr hLibModule);

        protected DT LoadFunction<DT>(string funcName) where DT : Delegate
        {
            var ptr = GetProcAddress(currentModule, funcName);
            return (DT)Marshal.GetDelegateForFunctionPointer(ptr, typeof(DT));
        }

        public abstract string DLL { get; }

        public abstract bool Load(string dllPath);
        public virtual void Unload()
        {
            if (IsLoaded)
            {
                FreeLibrary(currentModule);
            }
            currentModule = IntPtr.Zero;
        }

        public void Dispose()
        {
            Unload();
        }

        public abstract void OnEvent(EventType type, EventArguments arguments);
        public abstract TeamInfo GetTeamInfo();
        public abstract Wheel[] GetInstruction(Field field);
        public abstract Placement GetPlacement(Field field);
    }

    class StrategyDLL : StrategyDllBase
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
        delegate void GetPlacementDelegate(ref Native.Field field);
        GetPlacementDelegate _getPlacement;
        #endregion

        string _lastDllPath = null;
        public override string DLL => IsLoaded ? _lastDllPath : null;

        public const int MAX_STRING_LEN = 128;

        public StrategyDLL()
        {
        }

        public override bool Load(string dllPath)
        {
            if (IsLoaded)
            {
                return false;
            }
            _lastDllPath = dllPath;
            var hModule = LoadLibrary(dllPath);
            if (hModule == IntPtr.Zero)
            {
                return false;
            }
            currentModule = hModule;
            try
            {
                //BEGIN UNMANAGED FUNCTIONS
                _getTeamInfo = LoadFunction<GetTeamInfoDelegate>("GetTeamInfo");
                _onEvent = LoadFunction<OnEventDelegate>("OnEvent");
                _getInstruction = LoadFunction<GetInstructionDelegate>("GetInstruction");
                _getPlacement = LoadFunction<GetPlacementDelegate>("GetPlacement");
                //END UNMANAGED FUNCTIONS
            }
            catch
            {
                Unload();
                return false;
            }
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
                            throw new DLLException("OnEvent", e);
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
        public override TeamInfo GetTeamInfo()
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
                throw new DLLException("GetTeamInfo", e);
            }
            return new TeamInfo
            {
                TeamName = teamInfo.teamName
            };
        }

        public override Wheel[] GetInstruction(Field field)
        {
            if (_getInstruction == null)
            {
                throw new DllNotFoundException();
            }
            var nativeField = new Native.Field(field);
            try
            {
                _getInstruction(ref nativeField);
            }
            catch (Exception e)
            {
                throw new DLLException("GetInstruction", e);
            }
            return (from x in nativeField.SelfRobots select (Wheel)x.wheel).ToArray();
        }

        public override Placement GetPlacement(Field field)
        {
            if (_getPlacement == null)
            {
                throw new DllNotFoundException();
            }
            var nativeField = new Native.Field(field);
            try
            {
                _getPlacement(ref nativeField);
            }
            catch (Exception e)
            {
                throw new DLLException("GetPlacement", e);
            }
            return new Placement
            {
                Ball = (Ball)nativeField.ball,
                Robots = { from x in nativeField.SelfRobots select (Robot)x }
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
        public override string DLL => IsLoaded ? _lastDllPath : null;
        
        private readonly Placement placement = new Placement
        {
            Ball = new Ball {Position = new Vector2 {X = 50, Y = (float) 41.5}},
            Robots =
            {
                new Robot {Position = new Vector2 {X = (float) 90.5, Y = 42}, Wheel = new Wheel()},
                new Robot {Position = new Vector2 {X = 81, Y = 23}, Wheel = new Wheel()},
                new Robot {Position = new Vector2 {X = 81, Y = 61}, Wheel = new Wheel()},
                new Robot {Position = new Vector2 {X = 62, Y = 23}, Wheel = new Wheel()},
                new Robot {Position = new Vector2 {X = 62, Y = 61}, Wheel = new Wheel()},
            }
        };

        public override bool Load(string dllPath)
        {
            if (IsLoaded)
            {
                return false;
            }
            _lastDllPath = dllPath;
            var hModule = LoadLibrary(dllPath);
            if (hModule == IntPtr.Zero)
            {
                return false;
            }
            currentModule = hModule;
            try
            {
                //BEGIN UNMANAGED FUNCTIONS
                _create = LoadFunction<LegacyStrategyDelegate>("Create");
                _strategy = LoadFunction<LegacyStrategyDelegate>("Strategy");
                _destroy = LoadFunction<LegacyStrategyDelegate>("Destroy");
                //END UNMANAGED FUNCTIONS
            }
            catch
            {
                Unload();
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
            return true;
        }

        public override void Unload()
        {
            var env = new Native.Legacy.Environment();
            _destroy?.Invoke(ref env);

            base.Unload();

            _create = null;
            _strategy = null;
            _destroy = null;
        }

        JudgeResultEvent.Types.ResultType gameState = JudgeResultEvent.Types.ResultType.PlaceKick;
        Team whosball = Team.Nobody;

        public override Wheel[] GetInstruction(Field field)
        {
            if (_strategy == null)
            {
                throw new DllNotFoundException();
            }
            var env = new Native.Legacy.Environment(field, whosball, gameState);
            try
            {
                _strategy(ref env);
            }
            catch (Exception e)
            {
                throw new DLLException("Strategy", e);
            }

            return env.SelfRobots.Select(x => new Wheel()
            {
                LeftSpeed = (float) x.VelocityLeft,
                RightSpeed = (float) x.VelocityRight
            }).ToArray();
        }

        public override Placement GetPlacement(Field field)
        {
            return placement;
        }

        public override TeamInfo GetTeamInfo()
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

    class DLLException : Exception
    {
        string _functionName;
        public override string Message { get { return $"在 DLL 导出的函数 {_functionName} 中发生异常"; } }
        public Exception MaskedInnerException { get; }

        public DLLException(string functionName, Exception maskedInnerException)
        {
            _functionName = functionName;
            MaskedInnerException = maskedInnerException;
        }
    }

}
