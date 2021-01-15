import random
from typing import Tuple
from v5rpc import *


@catchall
def on_event(eventType: int, args: EventArguments):
    event = {
        0:lambda :print(args.JudgeResult.Reason),
        1:lambda :print("Match Start"),
        2:lambda :print("Match Stop"),
        3:lambda :print("First Half Start"),
        4:lambda :print("Second Half Start"),
        5:lambda :print("Overtime Start"),
        6:lambda :print("Penalty Shootout Start")
    }
    event[eventType]()


@catchall
def get_team_info(serverVersion: int) -> str:
    version = {
        0:"V1.0",
        1:"V1.1"
    }
    print(f'server rpc version: {version.get(serverVersion,"V1.0")}')
    return 'Python Strategy Server'


@catchall
def get_instruction(field: Field):
    field = Field(field)
    if(field.Tick % 10 == 0):
        print(f'tick = {field.Tick}')
    return [(125, -125), (125, -125), (125, -125), (125, -125), (125, -125)],0


@catchall
def get_placement(field: Field) -> List[Tuple[float, float, float]]:
    return [(random.randint(-50, 50), random.randint(-50, 50), 0) for _ in range(6)]