#!/usr/bin/env python3
"""Dependency-free behavioral reference for CounterBasedRandomV1."""

from __future__ import annotations

from dataclasses import dataclass

MASK64 = (1 << 64) - 1
OFFSET_BASIS = 14695981039346656037
FNV_PRIME = 1099511628211
VERSION = 1


def add_byte(value: int, byte: int) -> int:
    value ^= byte
    return (value * FNV_PRIME) & MASK64


def add_uint(value: int, item: int, byte_count: int) -> int:
    for shift in range(0, byte_count * 8, 8):
        value = add_byte(value, (item >> shift) & 0xFF)
    return value


def add_string(value: int, text: str) -> int:
    encoded = text.encode("utf-8")
    value = add_uint(value, len(encoded), 8)
    for byte in encoded:
        value = add_byte(value, byte)
    return value


def splitmix64(value: int) -> int:
    mixed = (value + 0x9E3779B97F4A7C15) & MASK64
    mixed = ((mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
    mixed = ((mixed ^ (mixed >> 27)) * 0x94D049BB133111EB) & MASK64
    return (mixed ^ (mixed >> 31)) & MASK64


@dataclass(frozen=True)
class Scope:
    domain: str
    owner: int
    purpose: int
    occurrence: int


class CounterBasedRandomV1:
    def __init__(self, seed: int):
        self.seed = seed

    def raw(self, scope: Scope, counter: int, lane: int = 0) -> int:
        value = OFFSET_BASIS
        value = add_uint(value, self.seed, 8)
        value = add_uint(value, VERSION, 4)
        value = add_string(value, scope.domain)
        value = add_uint(value, scope.owner, 8)
        value = add_uint(value, scope.purpose, 8)
        value = add_uint(value, scope.occurrence, 8)
        value = add_uint(value, counter, 4)
        value = add_uint(value, lane, 8)
        return splitmix64(value)

    def uint32(self, scope: Scope, counter: int) -> int:
        return self.raw(scope, counter) >> 32

    def uniform_below(self, scope: Scope, counter: int, bound: int) -> int:
        if bound <= 0:
            raise ValueError("bound")
        threshold = ((-bound) & MASK64) % bound
        lane = 0
        while True:
            sample = self.raw(scope, counter, lane)
            if sample >= threshold:
                return sample % bound
            lane += 1

    def range(self, scope: Scope, counter: int, minimum: int, maximum: int) -> int:
        if minimum >= maximum:
            raise ValueError("range")
        return minimum + self.uniform_below(scope, counter, maximum - minimum)

    def chance(self, scope: Scope, counter: int, numerator: int, denominator: int) -> bool:
        if denominator <= 0 or numerator < 0 or numerator > denominator:
            raise ValueError("probability")
        return numerator == denominator or (
            numerator != 0 and self.uniform_below(scope, counter, denominator) < numerator
        )


def main() -> None:
    seed = 0x0123456789ABCDEF
    random = CounterBasedRandomV1(seed)
    scope = Scope("colonist-generation", 42, 7, 3)

    assert random.raw(scope, 0) == 0xC2140C0AEA994494
    assert random.uint32(scope, 0) == 0xC2140C0A
    assert random.range(scope, 0, -1000, 1001) == 4
    assert random.chance(scope, 0, 17, 101) is False

    mirror = CounterBasedRandomV1(seed)
    for counter in range(64):
        assert random.raw(scope, counter) == mirror.raw(scope, counter)
        assert random.range(scope, counter, -1000, 1001) == mirror.range(scope, counter, -1000, 1001)

    incident = Scope("incident-selection", 77, 5, 9)
    food = Scope("food-production", 900, 12, 44)
    baseline = [random.raw(incident, counter) for counter in range(32)]
    perturbed_random = CounterBasedRandomV1(seed)
    perturbed = []
    for counter in range(32):
        for unrelated_counter in range(19):
            perturbed_random.raw(food, counter * 19 + unrelated_counter)
        perturbed.append(perturbed_random.raw(incident, counter))
    assert baseline == perturbed

    for counter in range(10_000):
        assert -7 <= random.range(scope, counter, -7, 8) < 8
        assert -(1 << 31) <= random.range(scope, counter, -(1 << 31), (1 << 31) - 1) < (1 << 31) - 1
        assert random.range(scope, counter, 0, 1) == 0

    assert all(not random.chance(scope, counter, 0, 100) for counter in range(128))
    assert all(random.chance(scope, counter, 100, 100) for counter in range(128))
    half = [random.chance(scope, counter, 1, 2) for counter in range(128)]
    assert any(half) and not all(half)

    print("random behavioral reference: PASS")


if __name__ == "__main__":
    main()
