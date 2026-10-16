"""Backtracking CSP for assigning restoration items to viable field sites."""
from io_utils import run


def assign(data):
    resources = data["resources"]
    locations = {item["id"]: item for item in data["locations"]}
    choices = {}
    for resource in resources:
        choices[resource["id"]] = [site_id for site_id, site in locations.items() if _fits(resource, site)]
    if any(not values for values in choices.values()):
        return {"success": False, "assignments": {}, "reason": "at least one resource has no compatible location", "attempts": 0}
    ordered = sorted(resources, key=lambda r: len(choices[r["id"]]))
    assignment, used = {}, set()
    attempts = [0]

    def backtrack(index):
        if index == len(ordered):
            return True
        resource = ordered[index]
        for site_id in choices[resource["id"]]:
            attempts[0] += 1
            site = locations[site_id]
            if site_id in used or any(
                site_id in locations[other].get("conflicts_with", []) or other in site.get("conflicts_with", [])
                for other in assignment.values() if other in locations
            ):
                continue
            assignment[resource["id"]] = site_id
            used.add(site_id)
            if backtrack(index + 1):
                return True
            used.remove(site_id)
            del assignment[resource["id"]]
        return False

    success = backtrack(0)
    return {"success": success, "assignments": assignment if success else {}, "attempts": attempts[0], "candidate_sites": choices}


def _fits(resource, site):
    kind = resource["type"]
    suitability = site.get("suitability", {})
    if kind not in suitability.get("resource_types", []):
        return False
    if site.get("habitat_restricted", False) and not resource.get("may_use_restricted_habitat", False):
        return False
    return float(site.get("soil", 0)) >= float(resource.get("minimum_soil", 0)) and float(site.get("water_distance", 999)) <= float(resource.get("maximum_water_distance", 999))


if __name__ == "__main__":
    run(assign)
