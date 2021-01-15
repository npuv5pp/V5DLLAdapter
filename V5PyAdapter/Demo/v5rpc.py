"""
This is types and helpers for strategies with PyV5Adapter.
"""

from typing import Optional, List


def catchall(fun):
    """Catch all exceptions in function."""

    def inner(*args):
        try:
            return fun(*args)
        except Exception as ex:
            print('An error is caught by @catchall:')
            print(ex)

    return inner

def hasattr(obj, attr: str) -> bool:
    attrs = dir(obj)
    return not attr in attrs

class Team:
    Self = 0
    Opponent = 1
    Nobody = 2


class EventType:
    JudgeResult = 0
    MatchStart = 1
    MatchStop = 2
    FirstHalfStart = 3
    SecondHalfStart = 4
    OvertimeStart = 5
    PenaltyShootoutStart = 6


class JudgeResultEvent:
    class ResultType:
        PlaceKick = 0
        GoalKick = 1
        PenaltyKick = 2
        FreeKickRightTop = 3
        FreeKickRightBot = 4
        FreeKickLeftTop = 5
        FreeKickLeftBot = 6

    Type: int  # ResultType
    OffensiveTeam: int  # Team
    Reason: str


class EventArguments:
    JudgeResult: Optional[JudgeResultEvent]


class Version:
    V1_0 = 0
    V1_1 = 1


class Vector2:
    def __init__(self, v) -> None:
        if hasattr(v, "x"):
            self.x = 0
        else:
            self.x = v.x

        if hasattr(v, "y"):
            self.y = 0
        else:
            self.y = v.y
    x: float
    y: float


class Wheel:
    def __init__(self, whell):
        if hasattr(whell, "LeftSpeed"):
            self.LeftSpeed = 0
        else:
            self.LeftSpeed = whell.LeftSpeed

        if hasattr(whell, "RightSpeed"):
            self.RightSpeed = 0
        else:
            self.RightSpeed = whell.RightSpeed
    LeftSpeed: float
    RightSpeed: float


class Robot:
    def __init__(self, robot):
        self.Position = Vector2(robot.Position)
        if hasattr(robot, "Rotation"):
            self.Rotation = 0
        else:
            self.Rotation=robot.Rotation
        self.Wheel = Wheel(robot.Wheel)
    Position: Vector2
    Rotation: float
    Wheel: Wheel


class Ball:
    def __init__(self, ball) -> None:
        self.Position = Vector2(ball.Position)
    Position: Vector2


class Field:
    def __init__(self, field):
        self.SelfRobots = []
        for i in field.SelfRobots:
            self.SelfRobots.append(Robot(i))
        self.OpponentRobots = []
        for i in field.OpponentRobots:
            self.OpponentRobots.append(Robot(i))
        self.Ball = Ball(field.Ball)
        self.Tick = field.Tick
    SelfRobots: List[Robot]
    OpponentRobots: List[Robot]
    Ball: Ball
    Tick: int

