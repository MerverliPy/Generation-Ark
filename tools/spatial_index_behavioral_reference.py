#!/usr/bin/env python3
"""Small dependency-free reference for Step 11 canonical spatial ordering."""


def canonical(operations):
    forward = {}
    for entity, cell in sorted(operations, key=lambda item: item[0]):
        if cell is None:
            forward.pop(entity, None)
        else:
            forward[entity] = cell
    reverse = {}
    for entity, cell in sorted(forward.items()):
        reverse.setdefault(cell, []).append(entity)
    return tuple(sorted(forward.items())), tuple((cell, tuple(entities)) for cell, entities in sorted(reverse.items()))


def main():
    first = canonical([(3, 2), (1, 0), (2, 0)])
    second = canonical([(2, 0), (3, 2), (1, 0)])
    assert first == second
    assert first == (((1, 0), (2, 0), (3, 2)), ((0, (1, 2)), (2, (3,))))
    moved = canonical([(1, 0), (1, 2), (2, 2), (2, None)])
    assert moved == (((1, 2),), ((2, (1,)),))
    print("spatial index behavioral reference: PASS")


if __name__ == "__main__":
    main()
