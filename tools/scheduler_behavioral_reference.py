#!/usr/bin/env python3
"""Behavioral reference for the deterministic scheduler ordering contract."""

from __future__ import annotations

from dataclasses import dataclass, replace
import heapq

PHASES = {
    "CommandApply": 0,
    "PreSimulation": 1,
    "ShipInfrastructure": 2,
    "ResourceNetworks": 3,
    "AgentState": 4,
    "AgentDecision": 5,
    "AgentAction": 6,
    "SocialAndInstitutions": 7,
    "Narrative": 8,
    "Commit": 9,
    "Diagnostics": 10,
}


@dataclass(frozen=True)
class Event:
    due: int
    phase: str
    priority: int
    sequence: int
    event_id: int
    payload: str
    interval: int | None = None

    def key(self) -> tuple[int, int, int, int, int]:
        return (self.due, PHASES[self.phase], self.priority, self.sequence, self.event_id)


class Scheduler:
    def __init__(self) -> None:
        self.current_tick = 0
        self.phase: str | None = None
        self.next_id = 1
        self.next_sequence = 1
        self.active: dict[int, Event] = {}
        self.heap: list[tuple[tuple[int, int, int, int, int], Event]] = []
        self.trace: list[str] = []

    def schedule(self, due: int, phase: str, priority: int, payload: str, interval: int | None = None) -> int:
        if due < self.current_tick:
            raise ValueError("past due")
        if interval is not None and interval <= 0:
            raise ValueError("invalid interval")
        if due == self.current_tick:
            if self.phase is None or PHASES[phase] <= PHASES[self.phase]:
                due += 1
        event = Event(due, phase, priority, self.next_sequence, self.next_id, payload, interval)
        self.next_id += 1
        self.next_sequence += 1
        self.active[event.event_id] = event
        heapq.heappush(self.heap, (event.key(), event))
        return event.event_id

    def cancel(self, event_id: int) -> bool:
        return self.active.pop(event_id, None) is not None

    def run_tick(self, on_phase=None) -> None:
        self.current_tick += 1
        for phase in PHASES:
            self.phase = phase
            self._execute_phase()
            if on_phase:
                on_phase(self, phase)
        self.phase = None
        if self._peek() is not None and self._peek().due <= self.current_tick:
            raise AssertionError("due event survived tick")

    def _peek(self) -> Event | None:
        while self.heap:
            _, event = self.heap[0]
            if self.active.get(event.event_id) == event:
                return event
            heapq.heappop(self.heap)
        return None

    def _execute_phase(self) -> None:
        while True:
            event = self._peek()
            if event is None or event.due > self.current_tick:
                return
            if PHASES[event.phase] > PHASES[self.phase]:
                return
            if event.phase != self.phase:
                raise AssertionError("event missed phase")
            heapq.heappop(self.heap)
            self.active.pop(event.event_id)
            self.trace.append(f"{event.payload}@{self.current_tick}:{self.phase}")
            if event.interval is not None:
                repeated = replace(event, due=event.due + event.interval)
                self.active[repeated.event_id] = repeated
                heapq.heappush(self.heap, (repeated.key(), repeated))


def validate() -> None:
    scheduler = Scheduler()
    scheduler.schedule(1, "PreSimulation", 20, "A")
    scheduler.schedule(1, "PreSimulation", 10, "B")
    scheduler.schedule(1, "PreSimulation", 10, "C")
    scheduler.run_tick()
    assert scheduler.trace == ["B@1:PreSimulation", "C@1:PreSimulation", "A@1:PreSimulation"]

    repeating = Scheduler()
    repeating.schedule(2, "Narrative", 0, "repeat", interval=3)
    for _ in range(11):
        repeating.run_tick()
    assert repeating.trace == [
        "repeat@2:Narrative",
        "repeat@5:Narrative",
        "repeat@8:Narrative",
        "repeat@11:Narrative",
    ]
    assert next(iter(repeating.active.values())).due == 14

    later = Scheduler()
    scheduled = False

    def later_phase(s: Scheduler, phase: str) -> None:
        nonlocal scheduled
        if phase == "AgentState" and not scheduled:
            scheduled = True
            s.schedule(s.current_tick, "Narrative", 0, "later")

    later.run_tick(later_phase)
    assert later.trace == ["later@1:Narrative"]

    same = Scheduler()
    scheduled = False

    def same_phase(s: Scheduler, phase: str) -> None:
        nonlocal scheduled
        if phase == "AgentState" and not scheduled:
            scheduled = True
            s.schedule(s.current_tick, "AgentState", 0, "same")

    same.run_tick(same_phase)
    assert same.trace == []
    same.run_tick()
    assert same.trace == ["same@2:AgentState"]

    cancelled = Scheduler()
    event_id = cancelled.schedule(1, "Narrative", 0, "cancelled")
    assert cancelled.cancel(event_id)
    assert not cancelled.cancel(event_id)
    cancelled.run_tick()
    assert cancelled.trace == []

    print("scheduler behavioral reference: PASS")


if __name__ == "__main__":
    validate()
