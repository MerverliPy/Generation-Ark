#!/usr/bin/env python3
from __future__ import annotations

import base64
import json
from dataclasses import dataclass

TEN_YEAR_TICKS = 5_184_000
FNV_OFFSET = 14695981039346656037
FNV_PRIME = 1099511628211
MASK = (1 << 64) - 1


def mix64(value: int) -> int:
    value = (value + 0x9E3779B97F4A7C15) & MASK
    value = ((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9) & MASK
    value = ((value ^ (value >> 27)) * 0x94D049BB133111EB) & MASK
    return (value ^ (value >> 31)) & MASK


def hash_u64(hash_value: int, value: int) -> int:
    for shift in range(0, 64, 8):
        hash_value ^= (value >> shift) & 0xFF
        hash_value = (hash_value * FNV_PRIME) & MASK
    return hash_value


@dataclass(frozen=True)
class Command:
    accepted_tick: int
    target_tick: int
    sequence: int
    command_id: str
    delta: int


class Scenario:
    def __init__(self) -> None:
        self.tick = 0
        self.seed = 0x0123456789ABCDEF
        self.state = 0xA5A5A5A55A5A5A5A
        self.random_counter = 0
        self.pending: list[Command] = []
        self.next_event_id = 2
        self.next_event_sequence = 1
        self.event = [1, 1440, 0, 0, 29, 1440]

    def submit(self, command: Command) -> None:
        assert command.accepted_tick == self.tick
        self.pending.append(command)

    def run_tick(self) -> None:
        self.tick += 1
        due = sorted(
            (item for item in self.pending if item.target_tick == self.tick),
            key=lambda item: (item.target_tick, item.sequence, item.command_id),
        )
        for command in due:
            self.state = (self.state + command.delta) & MASK
            self.pending.remove(command)
        if self.event[1] == self.tick:
            self.state = (self.state + self.event[4]) & MASK
            self.event = [
                self.next_event_id,
                self.event[1] + self.event[5],
                self.event[2],
                self.next_event_sequence,
                self.event[4],
                self.event[5],
            ]
            self.next_event_id += 1
            self.next_event_sequence += 1
        random_value = mix64(
            self.seed ^ self.tick ^ ((self.random_counter * 0x9E3779B97F4A7C15) & MASK)
        )
        self.random_counter += 1
        self.state = (
            self.state * 6364136223846793005
            + 1442695040888963407
            + random_value
        ) & MASK

    def checksum(self) -> int:
        value = FNV_OFFSET
        for item in (
            self.tick,
            self.seed,
            self.state,
            self.random_counter,
            self.next_event_id,
            self.next_event_sequence,
        ):
            value = hash_u64(value, item)
        return value

    def save(self) -> bytes:
        payload = {
            "tick": self.tick,
            "state": f"0x{self.state:016X}",
            "randomCounter": f"0x{self.random_counter:016X}",
            "pending": [item.__dict__ for item in sorted(
                self.pending,
                key=lambda item: (item.target_tick, item.sequence, item.command_id),
            )],
            "event": self.event,
            "nextEventId": self.next_event_id,
            "nextEventSequence": self.next_event_sequence,
        }
        return json.dumps(payload, separators=(",", ":"), sort_keys=False).encode()

    @classmethod
    def load(cls, payload: bytes) -> "Scenario":
        data = json.loads(payload)
        result = cls()
        result.tick = data["tick"]
        result.state = int(data["state"], 16)
        result.random_counter = int(data["randomCounter"], 16)
        result.pending = [Command(**item) for item in data["pending"]]
        result.event = data["event"]
        result.next_event_id = data["nextEventId"]
        result.next_event_sequence = data["nextEventSequence"]
        return result


def run(final_tick: int, save_tick: int | None = None) -> tuple[int, bytes | None]:
    commands = [
        Command(0, 10, 2, "command-b", 5),
        Command(0, 10, 1, "command-a", 3),
        Command(5, 12, 3, "command-c", -2),
        Command(100, 700, 4, "pending-through-save", 17),
        Command(800, 900, 5, "post-save", 11),
    ]
    scenario = Scenario()
    save_payload = None
    while scenario.tick < final_tick:
        for command in commands:
            if command.accepted_tick == scenario.tick:
                scenario.submit(command)
        scenario.run_tick()
        if save_tick is not None and scenario.tick == save_tick:
            save_payload = scenario.save()
            scenario = Scenario.load(save_payload)
    return scenario.checksum(), save_payload


def canonical_replay_vector() -> bytes:
    commands = [
        {
            "acceptedTick": 0,
            "targetTick": 10,
            "sequence": 2,
            "commandId": "command-b",
            "commandType": "state-delta-v1",
            "payloadBase64": base64.b64encode(b"5").decode(),
        },
        {
            "acceptedTick": 0,
            "targetTick": 10,
            "sequence": 1,
            "commandId": "command-a",
            "commandType": "state-delta-v1",
            "payloadBase64": base64.b64encode(b"3").decode(),
        },
    ]
    commands.sort(key=lambda item: (item["targetTick"], item["sequence"], item["commandId"]))
    replay = {
        "formatVersion": 1,
        "rootSeed": "0x0123456789ABCDEF",
        "buildVersion": "step8-foundation-r1",
        "finalTick": 10,
        "commands": commands,
        "checkpoints": [{"tick": 10, "checksum": "0x0000000000000000"}],
    }
    return json.dumps(replay, separators=(",", ":"), sort_keys=False).encode()



def frame_pattern_progress_reference() -> None:
    # Stable 144 FPS at 30 authoritative ticks/second legitimately produces
    # several zero-whole-tick frames while fractional backlog accumulates.
    tick = 0
    accumulated_ticks = 0.0
    tick_at_cycle_start = tick
    accumulated_at_cycle_start = accumulated_ticks
    cycles_without_progress = 0

    for _ in range(12):
        accumulated_ticks += (1.0 / 144.0) * 30.0
        whole_ticks = int(accumulated_ticks + 1e-9)
        tick += whole_ticks
        accumulated_ticks -= whole_ticks

        if tick == tick_at_cycle_start and accumulated_ticks == accumulated_at_cycle_start:
            cycles_without_progress += 1
        else:
            cycles_without_progress = 0
            tick_at_cycle_start = tick
            accumulated_at_cycle_start = accumulated_ticks

        assert cycles_without_progress < 2

    assert tick == 2

    # A genuinely inert pattern changes neither authoritative ticks nor backlog.
    tick = 0
    accumulated_ticks = 0.0
    tick_at_cycle_start = tick
    accumulated_at_cycle_start = accumulated_ticks
    cycles_without_progress = 0
    for _ in range(2):
        if tick == tick_at_cycle_start and accumulated_ticks == accumulated_at_cycle_start:
            cycles_without_progress += 1
        else:
            cycles_without_progress = 0
            tick_at_cycle_start = tick
            accumulated_at_cycle_start = accumulated_ticks
    assert cycles_without_progress == 2

def main() -> None:
    assert TEN_YEAR_TICKS == 5_184_000
    frame_pattern_progress_reference()
    canonical = canonical_replay_vector()
    assert json.dumps(json.loads(canonical), separators=(",", ":"), sort_keys=False).encode() == canonical
    uninterrupted, _ = run(2_000)
    resumed, payload = run(2_000, save_tick=400)
    assert payload is not None
    assert uninterrupted == resumed
    first, _ = run(10_000)
    second, _ = run(10_000)
    assert first == second
    print("replay continuity behavioral reference: PASS")


if __name__ == "__main__":
    main()
