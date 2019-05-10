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
    }
}
