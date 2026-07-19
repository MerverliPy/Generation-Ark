"""Executable behavioral mirror used only to validate milestone invariants when .NET is unavailable."""
from dataclasses import dataclass, field
from enum import IntEnum
from typing import Callable

class Speed(IntEnum):
    PAUSED=0; NORMAL=1; FAST=4; VERY_FAST=16; CHRONICLE=64; DEEP=256

@dataclass(order=True)
class Command:
    target: int
    sequence: int
    command_id: int
    key: str = field(compare=False)
    delta: int = field(compare=False)

class Model:
    def __init__(self):
        self.tick=0; self.speed=Speed.PAUSED; self.acc=0.0; self.counters={}; self.commands=[]
    def enqueue(self,c): self.commands.append(c); self.commands.sort()
    def run_tick(self):
        self.tick+=1
        while self.commands and self.commands[0].target==self.tick:
            c=self.commands.pop(0); self.counters[c.key]=self.counters.get(c.key,0)+c.delta
        self.counters['alpha']=self.counters.get('alpha',0)+3
        self.counters['beta']=self.counters.get('beta',0)+7
        self.counters['ordered']=self.counters.get('ordered',0)+2
        self.counters['ordered']=self.counters.get('ordered',0)+5
    def frame(self,dt,limit):
        if self.speed==Speed.PAUSED: self.acc=0; return 0
        self.acc += dt*30*int(self.speed)
        n=min(int(self.acc),limit)
        for _ in range(n): self.run_tick()
        self.acc-=n
        return n

def run(pattern,target):
    m=Model(); m.speed=Speed.VERY_FAST
    m.enqueue(Command(10,1,1,'alpha',50)); m.enqueue(Command(999,2,2,'beta',-100)); m.enqueue(Command(10000,3,3,'ordered',12))
    i=0
    while m.tick<target:
        m.frame(pattern[i%len(pattern)],target-m.tick); i+=1
    return m.tick, tuple(sorted(m.counters.items()))

a=run([1/60],20000)
b=run([1/144,1/24,.001,.075,1/90],20000)
c=run([.5,0,.002,1/30,.25],20000)
assert a==b==c, (a,b,c)

m=Model(); m.speed=Speed.DEEP
assert m.frame(1.0,10)==10 and m.acc>7000
assert m.frame(0.0,10)==10 and m.tick==20
print('Behavioral reference validation passed.')
