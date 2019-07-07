using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace V5DLLAdapter
{
    namespace Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct TeamInfo
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = StrategyDLL.MAX_STRING_LEN)]
            public string teamName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct JudgeResultEvent
        {
            public V5RPC.Proto.JudgeResultEvent.Types.ResultType type;
            public V5RPC.Proto.Team offensiveTeam;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = StrategyDLL.MAX_STRING_LEN)]
            public string reason;
        };

        struct Vector2
        {
            public Vector2(V5RPC.Proto.Vector2 obj)
            {
                x = obj.X;
                y = obj.Y;
            }
            public static explicit operator V5RPC.Proto.Vector2(Vector2 obj)
            {
                return new V5RPC.Proto.Vector2
                {
                    X = obj.x,
                    Y = obj.y
                };
            }
            public float x;
            public float y;
        }

        struct Ball
        {
            public Ball(V5RPC.Proto.Ball obj)
            {
                position = new Vector2(obj.Position);
            }
            public static explicit operator V5RPC.Proto.Ball(Ball obj)
            {
                return new V5RPC.Proto.Ball
                {
                    Position = (V5RPC.Proto.Vector2)obj.position
                };
            }
            public Vector2 position;
        }

        struct Wheel
        {
            public Wheel(V5RPC.Proto.Wheel obj)
            {
                leftSpeed = obj.LeftSpeed;
                rightSpeed = obj.RightSpeed;
            }
            public static explicit operator V5RPC.Proto.Wheel(Wheel obj)
            {
                return new V5RPC.Proto.Wheel
                {
                    LeftSpeed = obj.leftSpeed,
                    RightSpeed = obj.rightSpeed
                };
            }
            public float leftSpeed;
            public float rightSpeed;
        }

        struct Robot
        {
            public Robot(V5RPC.Proto.Robot obj)
            {
                position = new Vector2(obj.Position);
                rotation = obj.Rotation;
                wheel = new Wheel(obj.Wheel);
            }
            public static explicit operator V5RPC.Proto.Robot(Robot obj)
            {
                return new V5RPC.Proto.Robot
                {
                    Position = (V5RPC.Proto.Vector2)obj.position,
                    Rotation = obj.rotation,
                    Wheel = (V5RPC.Proto.Wheel)obj.wheel
                };
            }
            public Vector2 position;
            public float rotation;
            public Wheel wheel;
        }

        struct Field
        {
            public Field(V5RPC.Proto.Field obj)
            {
                SelfRobots = (from x in obj.SelfRobots select new Robot(x)).ToArray();
                opponentRobots = (from x in obj.OpponentRobots select new Robot(x)).ToArray();
                ball = new Ball(obj.Ball);
                tick = obj.Tick;
            }
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public Robot[] SelfRobots;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public Robot[] opponentRobots;
            public Ball ball;
            public int tick;
        }

        namespace Legacy
        {
            struct Vector3
            {
                public double x;
                public double y;
                public double z;

                private const double Inch2Cm = 2.54;
                private const double Cm2Inch = 1 / Inch2Cm;

                private const double MIDX = 50.1189; // 球场中点x坐标
                private const double MIDY = 41.8061; // 球场中点y坐标

                public Vector3(V5RPC.Proto.Vector2 obj)
                {
                    x = obj.X * Cm2Inch + MIDX;
                    y = obj.Y * Cm2Inch + MIDY;
                    z = 0;
                }

                public static Vector2 LegacyToProto(Vector3 vector)
                {
                    return new Vector2
                    {
                        x = (float)((vector.x - MIDX) * Inch2Cm),
                        y = (float)((vector.y - MIDY) * Inch2Cm),
                    };
                }
            }

            struct Robot
            {
                public Robot(V5RPC.Proto.Robot obj)
                {
                    Position = new Vector3(obj.Position);
                    Rotation = obj.Rotation;
                    VelocityLeft = obj.Wheel.LeftSpeed;
                    VelocityRight = obj.Wheel.RightSpeed;
                }
                public Legacy.Vector3 Position;
                public double Rotation;
                public double VelocityLeft, VelocityRight;
            }
            
            struct OpponentRobot
            {
                public OpponentRobot(V5RPC.Proto.Robot obj)
                {
                    Position = new Vector3(obj.Position);
                    Rotation = obj.Rotation;
                }
                public Legacy.Vector3 Position;
                public double Rotation;
            }

            struct Ball
            {
                public Legacy.Vector3 Position;
            }

            struct Bounds
            {
                public int Left, Right, Top, Bottom;
            }

            struct Environment
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
                public Legacy.Robot[] SelfRobots;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
                public Legacy.OpponentRobot[] OpponentRobots;
                public Legacy.Ball CurrentBall, LastBall, PredictedBall;
                public Legacy.Bounds FieldBounds, GoalBounds;
                public int GameState;
                public int WhosBall;
                public IntPtr UserData;

                public Environment(
                    V5RPC.Proto.Field field,
                    V5RPC.Proto.Team whosball,
                    V5RPC.Proto.JudgeResultEvent.Types.ResultType gamestate)
                {
                    WhosBall = (int)whosball;
                    GameState = (int)gamestate;
                    SelfRobots = new Legacy.Robot[5];
                    OpponentRobots = new Legacy.OpponentRobot[5];
                    for (int i = 0; i < 5; i++)
                    {
                        SelfRobots[i] = new Legacy.Robot(field.SelfRobots[i]);
                        OpponentRobots[i] = new Legacy.OpponentRobot(field.OpponentRobots[i]);
                    }
                    CurrentBall = new Legacy.Ball() { Position = new Legacy.Vector3(field.Ball.Position) };

                    UserData = IntPtr.Zero;

                    // Useless field, just become 0
                    LastBall = new Legacy.Ball();
                    PredictedBall = new Legacy.Ball();
                    FieldBounds = new Legacy.Bounds();
                    GoalBounds = new Legacy.Bounds();
                }
            }
        }
    }
}
