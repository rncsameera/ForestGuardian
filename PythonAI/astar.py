"""A* route search with explicit trail and terrain traversal costs."""
import heapq
import math
from io_utils import run


def _heuristic(node, goal, data):
    if node in data.get("heuristic", {}):
        return float(data["heuristic"][node])
    points = data.get("positions", {})
    if node in points and goal in points:
        a, b = points[node], points[goal]
        return math.hypot(float(a[0]) - float(b[0]), float(a[1]) - float(b[1])) * float(data.get("min_cost_per_unit", 1.0))
    return 0.0


def find_route(data):
    graph, start, goal = data["graph"], str(data["start"]), str(data["goal"])
    frontier, parent, costs = [(0.0, start)], {start: None}, {start: 0.0}
    expanded = []
    while frontier:
        _, node = heapq.heappop(frontier)
        if node in expanded:
            continue
        expanded.append(node)
        if node == goal:
            break
        for edge in graph.get(node, []):
            dest = str(edge["to"])
            new_cost = costs[node] + float(edge["cost"])
            if new_cost < costs.get(dest, math.inf):
                costs[dest], parent[dest] = new_cost, node
                heapq.heappush(frontier, (new_cost + _heuristic(dest, goal, data), dest))
    path = []
    if goal in parent:
        node = goal
        while node is not None:
            path.append(node)
            node = parent[node]
        path.reverse()
    return {"path": path, "cost": costs.get(goal), "reachable": bool(path), "expanded": expanded}


if __name__ == "__main__":
    run(find_route)
