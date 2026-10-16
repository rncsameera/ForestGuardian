"""Depth-first connected-component discovery for habitat regions."""
from io_utils import run


def explore(data):
    graph, start = data["graph"], str(data["start"])
    stack, seen, order = [start], set(), []
    while stack:
        node = stack.pop()
        if node in seen:
            continue
        seen.add(node)
        order.append(node)
        neighbors = graph.get(node, [])
        stack.extend(reversed([str(n.get("to")) if isinstance(n, dict) else str(n) for n in neighbors if (str(n.get("to")) if isinstance(n, dict) else str(n)) not in seen]))
    return {"region": data.get("region", "connected_habitat"), "start": start, "nodes": order, "count": len(order)}


if __name__ == "__main__":
    run(explore)
