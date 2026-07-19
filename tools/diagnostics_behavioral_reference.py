#!/usr/bin/env python3
"""Dependency-free behavioral reference for deterministic diagnostics."""

from dataclasses import dataclass
from typing import Optional

OFFSET = 14695981039346656037
PRIME = 1099511628211
MASK = (1 << 64) - 1


def add_byte(value: int, item: int) -> int:
    return ((value ^ item) * PRIME) & MASK


def add_u64(value: int, item: int) -> int:
    for shift in range(0, 64, 8):
        value = add_byte(value, (item >> shift) & 0xFF)
    return value


def add_i64(value: int, item: int) -> int:
    return add_u64(value, item & MASK)


def component_hash_i64(item: int) -> int:
    return add_i64(OFFSET, item)


@dataclass(frozen=True)
class Checkpoint:
    tick: int
    global_checksum: int
    components: tuple[tuple[str, int], ...]


def normalize_components(items: tuple[tuple[str, int], ...]) -> tuple[tuple[str, int], ...]:
    ordered = tuple(sorted(items, key=lambda item: item[0]))
    ids = [item[0] for item in ordered]
    if len(ids) != len(set(ids)):
        raise ValueError("duplicate component ID")
    return ordered


def component_differences(
    expected: Optional[Checkpoint], actual: Optional[Checkpoint]
) -> tuple[tuple[str, Optional[int], Optional[int]], ...]:
    expected_values = dict(expected.components if expected else ())
    actual_values = dict(actual.components if actual else ())
    result = []
    for component_id in sorted(set(expected_values) | set(actual_values)):
        expected_value = expected_values.get(component_id)
        actual_value = actual_values.get(component_id)
        if expected_value != actual_value:
            result.append((component_id, expected_value, actual_value))
    return tuple(result)


def first_divergence(
    expected: tuple[Checkpoint, ...], actual: tuple[Checkpoint, ...]
) -> tuple[int, Optional[Checkpoint], Optional[Checkpoint]] | None:
    for items in (expected, actual):
        for left, right in zip(items, items[1:]):
            if left.tick >= right.tick:
                raise ValueError("checkpoint ticks must be strictly increasing")

    expected_index = 0
    actual_index = 0
    while expected_index < len(expected) or actual_index < len(actual):
        expected_item = expected[expected_index] if expected_index < len(expected) else None
        actual_item = actual[actual_index] if actual_index < len(actual) else None
        if expected_item and actual_item and expected_item.tick == actual_item.tick:
            if (
                expected_item.global_checksum != actual_item.global_checksum
                or normalize_components(expected_item.components)
                != normalize_components(actual_item.components)
            ):
                return expected_item.tick, expected_item, actual_item
            expected_index += 1
            actual_index += 1
            continue
        if actual_item is None or (
            expected_item is not None and expected_item.tick < actual_item.tick
        ):
            return expected_item.tick, expected_item, None
        return actual_item.tick, None, actual_item
    return None


def main() -> None:
    assert component_hash_i64(10) == 16038372209008516879
    assert normalize_components((("world", 3), ("clock", 1), ("random", 2))) == (
        ("clock", 1),
        ("random", 2),
        ("world", 3),
    )

    expected = (
        Checkpoint(10, 100, (("world", 1),)),
        Checkpoint(20, 200, (("random", 2), ("world", 3))),
        Checkpoint(30, 300, (("world", 4),)),
    )
    actual = (
        Checkpoint(10, 100, (("world", 1),)),
        Checkpoint(20, 999, (("scheduler", 8), ("world", 4))),
        Checkpoint(30, 300, (("world", 4),)),
    )
    divergence = first_divergence(expected, actual)
    assert divergence is not None and divergence[0] == 20
    assert component_differences(divergence[1], divergence[2]) == (
        ("random", 2, None),
        ("scheduler", None, 8),
        ("world", 3, 4),
    )

    missing = first_divergence(expected, (actual[0], actual[2]))
    assert missing is not None and missing[0] == 20
    assert missing[1] is expected[1] and missing[2] is None

    print("diagnostics behavioral reference: PASS")


if __name__ == "__main__":
    main()
