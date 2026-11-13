#!/usr/bin/env python3
"""Export a member-level routing inventory from Siemens Openness XML docs."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path


FAMILY_RULES = (
    ("safety", (".safety", "safety")),
    ("hmi-unified", (".hmiunified", "hmiunified")),
    ("hmi-classic", (".hmi", "hmi.")),
    ("watch-tables", ("watchtable", "watchtables")),
    ("texts-graphics", ("textlist", "graphiclist", "graphics")),
    ("drive-startdrive", ("startdrive", "drivecontroller", "driveobject")),
    ("technology-objects", ("technologyobject", "technologyobjects", "motion")),
    ("opc", ("opcserver", "opcua", "opc")),
    ("libraries", (".libraries", "library")),
    ("collaboration-vci-teamcenter", ("multiuser", "teamcenter", "vci")),
    ("plc-tags-constants", (".tags", "plctag", "tagtable", "userconstant")),
    ("plc-external-sources", ("externalsource",)),
    ("plc-blocks", (".blocks", "plcblock", "blockgroup")),
    ("plc-types", (".types", "plctype", "typegroup")),
    ("online", ("onlineprovider", "onlineaccess", "diagnosticbuffer", "online")),
    ("devices", (".devices", "deviceitem", "devicecomposition", "devicegroup")),
    ("hardware", ("hardwarecatalog", ".hw", "network", "profinet", "ioaddress")),
    ("plc-software", ("plcsoftware", "softwarecontainer", "software")),
    ("projects-sessions", ("tiaportal", "project", "session", "portal")),
)

FAMILY_COVERAGE = {
    "projects-sessions": ("partial-family", ["GetCapabilities", "GetState", "ListProjects", "OpenProject", "SaveProject", "SaveAsProject", "CloseProject"]),
    "devices": ("partial-family", ["GetDevices", "GetDeviceInfo", "GetDeviceItemInfo", "DescribeEngineeringObject"]),
    "hardware": ("partial-family", ["GetHardwareNetworkInfo", "FindHardwareCatalog", "GetAccessibleDevices", "CompileHardware", "CreateHardwareDevice", "ConfigureIoAddress", "ConfigureHardwareNode", "CreateProfinetIoSystem", "AssignProfinetDevice", "ExportDeviceConfiguration"]),
    "plc-software": ("partial-family", ["GetSoftwareInfo", "GetSoftwareTree", "GetExternalSources", "CompileSoftware"]),
    "plc-blocks": ("partial-family", ["GetBlocks", "GetBlockInfo", "ReadBlockContent", "ImportBlock", "ExportBlock"]),
    "plc-types": ("partial-family", ["GetTypes", "GetTypeInfo", "ReadTypeContent", "ImportType", "ExportType"]),
    "plc-tags-constants": ("partial-family", ["GetPlcTagTables", "GetPlcTags"]),
    "plc-external-sources": ("partial-family", ["GetExternalSources"]),
    "online": ("partial-family", ["GetDeviceOnlineState", "GetOnlineConnectionOptions", "GoOnlineDevice", "GoOfflineDevice", "ReadPlcValues"]),
    "collaboration-vci-teamcenter": ("partial-family", ["ListProjects", "OpenProject"]),
}
SUMMARY_LIMIT = 240
OBJECT_HELPERS = {
    "equals", "gethashcode", "gettype", "memberwiseclone", "finalize", "tostring",
}


def route_member(member_id: str, kind: str, declaring_type: str) -> dict[str, object]:
    """Describe a generic route candidate without claiming per-version runtime support.

    XML documentation records names and signatures, not the reflection schema or
    runtime availability of the exact member. `implementedRoute` therefore names
    the MCP dispatch path only; `bridgePotential` says a member kind can be
    represented by that path, subject to the worker's generated schema and value
    codec. It is not an availability assertion.
    """
    signature = member_id.split(":", 1)[-1]
    head = signature.split("(", 1)[0]
    name = head.rsplit(".", 1)[-1]
    lower_name = name.casefold()

    if declaring_type.startswith("Siemens.Engineering.Private."):
        return {"routeDisposition": "excluded", "implementedRoute": None,
                "bridgePotential": False, "unsupportedReason": "privateNamespace"}

    if kind in ("namespace", "type"):
        return {"routeDisposition": "excluded", "implementedRoute": None,
                "bridgePotential": False, "unsupportedReason": "metadataOnly"}
    if kind == "property":
        return {"routeDisposition": "implementedRoute", "implementedRoute": "ReadApiProperty",
                "bridgePotential": True, "unsupportedReason": None}
    if kind == "method":
        if lower_name.startswith("get_"):
            route = "ReadApiProperty"
        elif lower_name.startswith("set_"):
            route = "WriteApiProperty"
        elif lower_name.startswith("add_") or lower_name.startswith("remove_"):
            route = "SubscribeApiEvent" if lower_name.startswith("add_") else "UnsubscribeApiEvent"
        elif lower_name == "getservice":
            route = "GetApiService"
        elif lower_name == "invoke":
            route = "InvokeEngineeringAction"
        elif lower_name == "dispose":
            return {"routeDisposition": "bridgePotential", "implementedRoute": "ReleaseApiObject",
                    "bridgePotential": True, "unsupportedReason": "issuedSiemensDisposableOnlyPortalLifetimeManagedSeparately"}
        elif lower_name in OBJECT_HELPERS or lower_name.startswith("op_"):
            return {"routeDisposition": "excluded", "implementedRoute": None,
                    "bridgePotential": False, "unsupportedReason": "inheritedOrOperatorHelper"}
        elif lower_name in (".ctor", "ctor", "#ctor"):
            return {"routeDisposition": "bridgePotential", "implementedRoute": "CreateApiObject",
                    "bridgePotential": True, "unsupportedReason": "constructorRequiresSupportedValueCodec"}
        else:
            route = "InvokeApiMethod"
        return {"routeDisposition": "implementedRoute", "implementedRoute": route,
                "bridgePotential": True, "unsupportedReason": None}
    if kind == "event":
        return {"routeDisposition": "implementedRoute", "implementedRoute": "SubscribeApiEvent",
                "bridgePotential": True, "unsupportedReason": None}
    if kind == "field":
        return {"routeDisposition": "implementedRoute", "implementedRoute": "ReadApiField",
                "bridgePotential": True, "unsupportedReason": None}
    return {"routeDisposition": "excluded", "implementedRoute": None,
            "bridgePotential": False, "unsupportedReason": "unsupportedXmlMemberKind"}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def classify_member(member_id: str) -> tuple[str, str, str]:
    """Return (kind, declaring type, family) from a compiler XML member ID."""
    if ":" not in member_id:
        return "unknown", "", "unclassified"
    kind_code, signature = member_id.split(":", 1)
    if len(kind_code) != 1 or not signature:
        return "unknown", "", "unclassified"
    kind = {"N": "namespace", "T": "type", "M": "method", "P": "property", "F": "field", "E": "event"}.get(kind_code, "other")
    signature_head = signature.split("(", 1)[0]
    if kind in ("namespace", "type"):
        declaring_type = signature_head
    else:
        declaring_type, _, _member_name = signature_head.rpartition(".")
        if not declaring_type:
            declaring_type = signature_head
    searchable = signature_head.casefold()
    family = "unclassified"
    for candidate, tokens in FAMILY_RULES:
        if any(token in searchable for token in tokens):
            family = candidate
            break
    return kind, declaring_type, family


def summary_text(member: ET.Element) -> str:
    node = member.find("summary")
    if node is None:
        return ""
    text = " ".join(" ".join(node.itertext()).split())
    return text if len(text) <= SUMMARY_LIMIT else text[: SUMMARY_LIMIT - 1].rstrip() + "…"


def parse_documentation(path: Path, tia_version: int, documentation_root: Path, include_summary: bool = False) -> list[dict[str, object]]:
    """Stream documented XML members without loading or invoking Siemens assemblies."""
    records: list[dict[str, object]] = []
    for _, element in ET.iterparse(path, events=("end",)):
        if element.tag != "member":
            continue
        member_id = element.get("name", "").strip()
        if not member_id:
            element.clear()
            continue
        kind, declaring_type, family = classify_member(member_id)
        coverage, _tools = FAMILY_COVERAGE.get(family, ("uncovered" if family != "unclassified" else "unclassified", []))
        route = route_member(member_id, kind, declaring_type)
        record = {
            "memberId": member_id,
            "memberKind": kind,
            "declaringType": declaring_type,
            "family": family,
            "familyCoverage": coverage,
            **route,
            "runtimeSupport": "notAssertedByXmlInventory",
            "apiVersion": f"V{tia_version}",
            "documentationFile": path.relative_to(documentation_root).as_posix(),
        }
        if include_summary:
            record["summary"] = summary_text(element)
        records.append(record)
        element.clear()
    return records


def inventory(documentation_dir: Path, tia_version: int, reference_dir: Path | None = None, include_summary: bool = False) -> dict[str, object]:
    docs = sorted(path for path in documentation_dir.rglob("Siemens.Engineering*.xml") if path.is_file())
    if not docs:
        raise ValueError(f"No Siemens.Engineering*.xml documentation files found under {documentation_dir}")
    members = [record for path in docs for record in parse_documentation(path, tia_version, documentation_dir, include_summary)]
    counts = Counter(record["family"] for record in members)
    documentation_counts = Counter(record["documentationFile"] for record in members)
    family_summary = []
    for family, count in sorted(counts.items()):
        coverage, tools = FAMILY_COVERAGE.get(family, ("uncovered" if family != "unclassified" else "unclassified", []))
        family_summary.append({"family": family, "documentedMemberCount": count, "coverage": coverage, "mcpTools": tools})
    assemblies = []
    assemblies_by_stem: dict[str, list[dict[str, str]]] = {}
    if reference_dir is not None:
        for path in sorted(reference_dir.rglob("Siemens.Engineering*.dll")):
            if not path.is_file():
                continue
            entry = {"file": path.relative_to(reference_dir).as_posix(), "sha256": sha256(path)}
            assemblies.append(entry)
            assemblies_by_stem.setdefault(path.stem.casefold(), []).append(entry)
    return {
        "schemaVersion": "tia-publicapi-inventory/2",
        "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "apiVersion": f"V{tia_version}",
        "scope": "Documented member IDs found in supplied Siemens.Engineering XML documentation. This is not proof that undocumented public members are absent.",
        "coverageMethod": "Per-member routing classification from XML member IDs. implementedRoute names a generic MCP dispatch path; bridgePotential marks member kinds representable in principle. Neither field asserts that this exact member is in the worker's runtime-generated schema or callable for this TIA version.",
        "documentationFiles": [
            {
                "file": path.relative_to(documentation_dir).as_posix(),
                "sha256": sha256(path),
                "documentedMemberCount": documentation_counts[path.relative_to(documentation_dir).as_posix()],
                "matchingReferenceAssemblies": assemblies_by_stem.get(path.stem.casefold(), []),
            }
            for path in docs
        ],
        "referenceAssemblies": assemblies,
        "referenceAssemblyNote": "Assemblies are identified and hashed only; this generator does not load, reflect over, or invoke Siemens code.",
        "familySummary": family_summary,
        "members": sorted(members, key=lambda item: (str(item["memberId"]), str(item["documentationFile"]))),
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--documentation-dir", required=True, type=Path, help="Root containing Siemens.Engineering*.xml documentation files")
    parser.add_argument("--tia-version", required=True, type=int, choices=range(17, 22), help="Exact Siemens API major version")
    parser.add_argument("--reference-dir", type=Path, help="Optional root containing exact Siemens.Engineering*.dll references to identify and hash")
    parser.add_argument("--output", required=True, type=Path, help="JSON inventory destination")
    parser.add_argument("--include-summary", action="store_true", help="Include short XML summary snippets. Omit for distributable inventory artifacts.")
    args = parser.parse_args(argv)
    try:
        result = inventory(args.documentation_dir, args.tia_version, args.reference_dir, args.include_summary)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\r\n")
    except (OSError, ET.ParseError, ValueError) as error:
        print(f"Inventory export failed: {error}", file=sys.stderr)
        return 2
    print(f"Wrote {len(result['members'])} documented member records to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
