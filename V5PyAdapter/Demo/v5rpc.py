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
    x: float
    y: float


class Wheel:
    LeftSpeed: float
    RightSpeed: float


class Robot:
    Sosition: Vector2
    Rotation: float
    Wheel: Wheel


class Ball:
    Position: Vector2


class Field:
    SelfRobots: List[Robot]
    OpponentRobots: List[Robot]
    Ball: Ball
    Tick: int

