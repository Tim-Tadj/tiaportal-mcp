import importlib.util
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPOSITORY_ROOT / "tools" / "export_public_api_inventory.py"
FIXTURE = Path(__file__).parent / "fixtures" / "Siemens.Engineering.xml"
SPEC = importlib.util.spec_from_file_location("public_api_inventory", MODULE_PATH)
INVENTORY = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(INVENTORY)


class PublicApiInventoryTests(unittest.TestCase):
    def test_parses_documented_member_ids_and_maps_families_conservatively(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            docs = root / "docs"
            docs.mkdir()
            fixture = docs / FIXTURE.name
            fixture.write_bytes(FIXTURE.read_bytes())

            records = INVENTORY.parse_documentation(fixture, 19, docs, include_summary=True)

        self.assertEqual(11, len(records))
        self.assertEqual("T:Siemens.Engineering.Project", records[0]["memberId"])
        self.assertEqual("projects-sessions", records[0]["family"])
        self.assertEqual("partial-family", records[0]["familyCoverage"])
        self.assertEqual("plc-blocks", records[1]["family"])
        self.assertEqual("Siemens.Engineering.SW.Blocks.PlcBlock", records[1]["declaringType"])
        self.assertEqual("hmi-unified", records[2]["family"])
        self.assertEqual("uncovered", records[2]["familyCoverage"])
        self.assertEqual("safety", records[3]["family"])
        self.assertEqual("unclassified", records[4]["familyCoverage"])
        self.assertEqual("A project & its sessions.", records[0]["summary"])
        self.assertEqual("V19", records[0]["apiVersion"])
        routes = {record["memberId"]: record for record in records}
        self.assertEqual("ReadApiProperty", routes["P:Siemens.Engineering.Project.Name"]["implementedRoute"])
        self.assertEqual("InvokeApiMethod", routes["M:Siemens.Engineering.Project.Save"]["implementedRoute"])
        self.assertEqual("WriteApiProperty", routes["M:Siemens.Engineering.Project.set_Name(System.String)"]["implementedRoute"])
        self.assertEqual("excluded", routes["M:Siemens.Engineering.Project.ToString"]["routeDisposition"])
        self.assertEqual("SubscribeApiEvent", routes["E:Siemens.Engineering.Project.Changed"]["implementedRoute"])
        self.assertEqual("ReadApiField", routes["F:Siemens.Engineering.Project.Version"]["implementedRoute"])
        self.assertEqual("notAssertedByXmlInventory", routes["M:Siemens.Engineering.Project.Save"]["runtimeSupport"])

    def test_inventory_hashes_modular_docs_and_matching_reference_assemblies(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            docs = root / "docs"
            references = root / "references"
            docs.mkdir()
            references.mkdir()
            (docs / FIXTURE.name).write_bytes(FIXTURE.read_bytes())
            assembly = references / "Siemens.Engineering.dll"
            assembly.write_bytes(b"fixture reference identity")

            result = INVENTORY.inventory(docs, 19, references)

        self.assertEqual("V19", result["apiVersion"])
        self.assertEqual("Siemens.Engineering.xml", result["documentationFiles"][0]["file"])
        self.assertEqual("Siemens.Engineering.dll", result["documentationFiles"][0]["matchingReferenceAssemblies"][0]["file"])
        self.assertTrue(result["documentationFiles"][0]["sha256"])
        self.assertTrue(result["referenceAssemblies"][0]["sha256"])
        self.assertEqual(11, result["documentationFiles"][0]["documentedMemberCount"])


if __name__ == "__main__":
    unittest.main()
