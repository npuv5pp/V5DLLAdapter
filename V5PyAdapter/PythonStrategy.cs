using System;
using V5RPC;
using V5RPC.Proto;
using Python.Runtime;

namespace V5PyAdapter
{
    class PythonStrategy : IStrategy
    {
        dynamic strategy;

        public PythonStrategy(string name)
        {
            using (Py.GIL())
            {
                dynamic sys = Py.Import("sys");
                sys.path.append(".");

                strategy = Py.Import(name);
            }
        }

        public (Wheel[], ControlInfo) GetInstruction(Field field)
        {
            using (Py.GIL())
            {
                dynamic result = strategy.get_instruction(field);
                return result;
            }
        }

        public Placement GetPlacement(Field field)
        {
            using (Py.GIL())
            {
                dynamic result = strategy.get_placement(field);
                return result;
            }
        }

        public TeamInfo GetTeamInfo(ServerInfo info)
        {
            using (Py.GIL())
            {
                string teamName = strategy.get_team_info();
                return new TeamInfo { TeamName = teamName };
            }
        }

        public void OnEvent(EventType type, EventArguments arguments)
        {
            using (Py.GIL())
            {
                strategy.on_event(type, arguments);
            }
        }
    }
}
