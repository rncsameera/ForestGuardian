"""Breadth-first STRIPS-style goal planner for pond restoration tasks."""
from collections import deque
from io_utils import run


def plan(data):
    start = frozenset(data["initial_facts"])
    goal = set(data["goal"])
    actions = data["actions"]
    queue, parent = deque([start]), {start: None}
    via = {}
    solved = None
    while queue:
        state = queue.popleft()
        if goal.issubset(state):
            solved = state
            break
        for action in actions:
            if not set(action.get("preconditions", [])).issubset(state):
                continue
            next_state = frozenset((set(state) - set(action.get("remove", []))) | set(action.get("add", [])))
            if next_state not in parent:
                parent[next_state], via[next_state] = state, action["name"]
                queue.append(next_state)
    sequence = []
    if solved is not None:
        node = solved
        while parent[node] is not None:
            sequence.append(via[node])
            node = parent[node]
        sequence.reverse()
    return {"success": solved is not None, "plan": sequence, "goal": sorted(goal), "final_facts": sorted(solved) if solved is not None else []}


if __name__ == "__main__":
    run(plan)
