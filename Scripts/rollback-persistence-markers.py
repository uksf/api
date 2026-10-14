import argparse
import sys

from pymongo import MongoClient

POLYLINE = "POLYLINE"


def legacy_marker(marker):
    if isinstance(marker, list):
        return marker
    position = list(marker.get("position") or [])
    if marker.get("shape") == POLYLINE:
        return [marker.get("name", ""), position, POLYLINE, marker.get("colour", ""), marker.get("alpha", 0.0), list(marker.get("polyline") or [])]
    return [
        marker.get("name", ""),
        position,
        marker.get("shape", ""),
        marker.get("type"),
        marker.get("brush"),
        list(marker.get("size") or []),
        marker.get("colour", ""),
        marker.get("alpha", 0.0),
        marker.get("direction") if marker.get("direction") is not None else 0,
        marker.get("text"),
    ]


def rollback(collection, apply):
    changed = 0
    for document in collection.find({"markers": {"$elemMatch": {"$type": "object"}}}, {"markers": 1}):
        legacy = [legacy_marker(marker) for marker in document["markers"]]
        changed += 1
        if apply:
            collection.update_one({"_id": document["_id"], "markers": document["markers"]}, {"$set": {"markers": legacy}})
    return changed


def self_check():
    standard = {"name": "m", "position": [1.0, 2.0], "shape": "ICON", "type": "hd_dot", "brush": "Solid", "size": [1.0, 1.0], "colour": "ColorRed", "alpha": 1.0, "text": "t"}
    assert legacy_marker(standard) == ["m", [1.0, 2.0], "ICON", "hd_dot", "Solid", [1.0, 1.0], "ColorRed", 1.0, 0, "t"]
    line = {"name": "l", "position": [0.0, 0.0], "shape": POLYLINE, "colour": "ColorBlue", "alpha": 0.5, "polyline": [1.0, 2.0, 3.0, 4.0]}
    assert legacy_marker(line) == ["l", [0.0, 0.0], POLYLINE, "ColorBlue", 0.5, [1.0, 2.0, 3.0, 4.0]]
    assert legacy_marker(["already", "legacy"]) == ["already", "legacy"]


def main():
    parser = argparse.ArgumentParser(description="Convert persistence markers written by migration 14 back to the positional arrays that API builds before 0af218eb read.")
    parser.add_argument("--uri", required=True, help="Mongo connection string including the database name")
    parser.add_argument("--collection", default="persistenceSessions")
    parser.add_argument("--apply", action="store_true", help="write the change; without this flag the script only counts documents")
    args = parser.parse_args()

    self_check()
    database_name = args.uri.rsplit("/", 1)[1].split("?", 1)[0]
    collection = MongoClient(args.uri)[database_name][args.collection]
    changed = rollback(collection, args.apply)
    print(f"{'converted' if args.apply else 'would convert'} {changed} documents in {database_name}.{args.collection}")


if __name__ == "__main__":
    sys.exit(main())
