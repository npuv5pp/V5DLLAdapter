using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using V5RPC.Proto;
using static V5RPC.Proto.JudgeResultEvent.Types;

namespace V5DLLAdapter
{
    namespace Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct TeamInfo
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = StrategyDll.MAX_STRING_LEN)]
            public string teamName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct JudgeResultEvent
        {
            public ResultType type;
            public V5RPC.Proto.Team offensiveTeam;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = StrategyDll.MAX_STRING_LEN)]
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

            public Robot Reverse()
            {
                return new Robot
                {
                    position = new Vector2 { x = -position.x, y = -position.y },
                    rotation = (float)FlipRotation(rotation),
                    wheel = wheel,
                };
            }

            public static double FlipRotation(double rotation)
            {
                return rotation >= 0 ? rotation - 180 : rotation + 180;
            }
            public Vector2 position;
            public float rotation;
            public Wheel wheel;
        }

        struct Field
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public Robot[] SelfRobots;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public Robot[] opponentRobots;
            public Ball ball;
            public int tick;
            
            public Field(V5RPC.Proto.Field obj)
            {
                SelfRobots = obj.SelfRobots.Select(x => new Robot(x)).ToArray();
                opponentRobots = obj.OpponentRobots.Select(x => new Robot(x)).ToArray();
                ball = new Ball(obj.Ball);
                tick = obj.Tick;
            }

            public void Reverse()
            {
                SelfRobots = SelfRobots.Select(x => x.Reverse()).ToArray();
                opponentRobots = opponentRobots.Select(x => x.Reverse()).ToArray();
                ball.position = new Vector2 {x = -ball.position.x, y = -ball.position.y};
            }
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

                public Vector3(Native.Vector2 obj)
                {
                    x = obj.x * Cm2Inch + MIDX;
                    y = obj.y * Cm2Inch + MIDY;
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

                public static Vector3 operator -(Vector3 vector)
                {
                    return new Vector3 { x = -vector.x, y = -vector.y, z = -vector.z};
                }
            }

            struct Robot
            {
                public Robot(Native.Robot obj)
                {
                    Position = new Vector3(obj.position);
                    Rotation = obj.rotation;
                    VelocityLeft = obj.wheel.leftSpeed;
                    VelocityRight = obj.wheel.rightSpeed;
                }
                public Legacy.Vector3 Position;
                public double Rotation;
                public double VelocityLeft, VelocityRight;
            }
            
            struct OpponentRobot
            {
                public OpponentRobot(Native.Robot obj)
                {
                    Position = new Vector3(obj.position);
                    Rotation = obj.rotation;
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

                public Environment(Native.Field field, V5RPC.Proto.Team whosball, ResultType gamestate, IntPtr userData)
                {
                    WhosBall = ToLegacyWhosball(whosball);
                    GameState = ToLegacyGameState(gamestate);
                    SelfRobots = new Legacy.Robot[5];
                    OpponentRobots = new Legacy.OpponentRobot[5];
                    for (int i = 0; i < 5; i++)
                    {
                        SelfRobots[i] = new Legacy.Robot(field.SelfRobots[i]);
                        OpponentRobots[i] = new Legacy.OpponentRobot(field.opponentRobots[i]);
                    }
                    CurrentBall = new Legacy.Ball() { Position = new Legacy.Vector3(field.ball.position) };

                    UserData = userData;

                    // Useless field, just become 0
                    LastBall = new Legacy.Ball();
                    PredictedBall = new Legacy.Ball();
                    FieldBounds = new Legacy.Bounds();
                    GoalBounds = new Legacy.Bounds();
                }

                private static int ToLegacyWhosball(V5RPC.Proto.Team whosball)
                {
                    switch (whosball)
                    {
                        case Team.Self:
                            return 1;
                        case Team.Opponent:
                            return 2;
                        case Team.Nobody:
                            return 0;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(whosball), whosball, null);
                    }
                }

                private static int ToLegacyGameState(ResultType gamestate)
                {
                    switch (gamestate)
                    {
                        case ResultType.PlaceKick:
                            return 2;
                        case ResultType.GoalKick:
                            return 5;
                        case ResultType.PenaltyKick:
                            return 3;
                        case ResultType.FreeKickRightTop:
                        case ResultType.FreeKickRightBot:
                        case ResultType.FreeKickLeftTop:
                        case ResultType.FreeKickLeftBot:
                            return 1;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(gamestate), gamestate, null);
                    }
                }

                public void Reverse()
                {
                    for (int i = 0; i < 5; i++)
                    {
                        SelfRobots[i].Position = -SelfRobots[i].Position;
                        SelfRobots[i].Rotation = Native.Robot.FlipRotation(SelfRobots[i].Rotation);
                        OpponentRobots[i].Position = -OpponentRobots[i].Position;
                        OpponentRobots[i].Rotation = Native.Robot.FlipRotation(OpponentRobots[i].Rotation);
                    }

                    CurrentBall = new Ball {Position = -CurrentBall.Position};
                }
            }
        }
    }
}
