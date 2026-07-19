#!/usr/bin/env python3
"""Dependency-free behavioral reference for the Step 7 Unity adapter."""

from __future__ import annotations

import math
from dataclasses import dataclass

SUPPORTED = {0, 1, 4, 16, 64, 256}
EPSILON = 1e-9


@dataclass(frozen=True)
class Result:
    executed: int
    backlog: int
    fraction: float


class Accumulator:
    def __init__(self, budget: int, ticks_per_second: float = 30.0) -> None:
        if budget <= 0:
            raise ValueError("budget")
        self.budget = budget
        self.ticks_per_second = ticks_per_second
        self.value = 0.0

    def advance(self, delta: float, speed: int, paused: bool = False) -> Result:
        if not math.isfinite(delta) or delta < 0:
            raise ValueError("delta")
        if speed not in SUPPORTED:
            raise ValueError("speed")
        if not paused and speed != 0:
            self.value += delta * self.ticks_per_second * speed
        whole = math.floor(self.value + EPSILON)
        executed = min(whole, self.budget) if not paused and speed != 0 else 0
        self.value -= executed
        backlog = math.floor(self.value + EPSILON)
        return Result(executed, backlog, self.value - backlog)


def test_fractional() -> None:
    acc = Accumulator(100)
    first = acc.advance(1 / 60, 1)
    second = acc.advance(1 / 60, 1)
    assert first == Result(0, 0, 0.5)
    assert second.executed == 1
    assert abs(second.fraction) < 1e-8


def test_backlog_and_pause() -> None:
    acc = Accumulator(7)
    first = acc.advance(1.0, 4)
    assert first.executed == 7 and first.backlog == 113
    paused = acc.advance(5.0, 4, paused=True)
    assert paused.executed == 0 and paused.backlog == 113
    total = first.executed
    while acc.value >= 1.0 - EPSILON:
        total += acc.advance(0.0, 4).executed
    assert total == 120


def test_patterns() -> None:
    def run(pattern: list[float]) -> int:
        acc = Accumulator(10_000)
        return sum(acc.advance(delta, 1).executed for delta in pattern)

    stable = run([1 / 60] * 120)
    irregular = run([0.10, 0.03, 0.07, 0.20, 0.40, 0.50, 0.70])
    assert stable == irregular == 60


def presentation_interval(speed: int, catching_up: bool) -> int:
    interval = {1: 1, 4: 1, 16: 2, 64: 4, 256: 8}[speed]
    return min(interval * 2, 32) if catching_up else interval


def test_presentation() -> None:
    assert presentation_interval(1, False) == 1
    assert presentation_interval(256, False) == 8
    assert presentation_interval(256, True) == 16


def main() -> None:
    test_fractional()
    test_backlog_and_pause()
    test_patterns()
    test_presentation()
    print("unity adapter behavioral reference: PASS")


if __name__ == "__main__":
    main()
