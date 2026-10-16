"""Breadth-first search for nearest reachable ForestGuardian field site."""
from collections import deque
from io_utils import run


def search(data):
    graph, start = data["graph"], str(data["start"])
    targets = {str(item) for item in data["targets"]}
    queue, parent, visited = deque([start]), {start: None}, []
    found = None
    while queue:
        node = queue.popleft()
        visited.append(node)
        if node in targets:
            found = node
            break
        for neighbor in graph.get(node, []):
            neighbor = str(neighbor.get("to")) if isinstance(neighbor, dict) else str(neighbor)
            if neighbor not in parent:
                parent[neighbor] = node
                queue.append(neighbor)
    path = []
    if found is not None:
        node = found
        while node is not None:
            path.append(node)
            node = parent[node]
        path.reverse()
    return {"target": found, "path": path, "distance": len(path) - 1 if path else None, "visited": visited}


if __name__ == "__main__":
    run(search)
