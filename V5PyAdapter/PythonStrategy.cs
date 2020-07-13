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
                sys.path.insert(0,"");
                strategy = Py.Import(name);                
            }
        }

        public (Wheel[], ControlInfo) GetInstruction(Field field)
        {
            using (Py.GIL())
            {
                dynamic result = strategy.get_instruction(field);
                Wheel[] wheels = new Wheel[5];
                for (int index = 0; index < 5; index++)
                {
                    wheels[index] = new Wheel()
                    {
                        LeftSpeed = result[0][index][0],
                        RightSpeed = result[0][index][1]
                    };
                }
                ControlInfo controlInfo = new ControlInfo
                {
                    Command = result[1]
                };
                return (wheels,controlInfo);
            }
        }

        public Placement GetPlacement(Field field)
        {
            using (Py.GIL())
            {
                dynamic result = strategy.get_placement(field);
                V5RPC.Proto.Placement placement = new Placement();
                for (int index = 0; index < 5; index++)
                {
                    placement.Robots.Add(new Robot
                    {
                        Position = new Vector2 { X = result[index][0], Y = result[index][1] },
                        Rotation = result[index][2]
                    });
                }
                placement.Ball = new Ball {
                    Position = new Vector2 { X = result[5][0], Y = result[5][1] }
                };
                return placement;
            }
        }

        public TeamInfo GetTeamInfo(V5RPC.Proto.Version info)
        {
            using (Py.GIL())
            {
                string teamName = strategy.get_team_info(info);
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
