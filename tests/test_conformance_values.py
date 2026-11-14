"""X02 §3 logical encodings; these are format checks, not original-game captures."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import conformance_values as values


class LogicalValuesTests(unittest.TestCase):
    def test_every_integer_width_has_exact_bounds_and_spelling(self):
        for bits in (8, 16, 32, 64):
            for signed in (False, True):
                kind = ("i" if signed else "u") + str(bits)
                low, high = (-(1 << (bits - 1)), (1 << (bits - 1)) - 1) if signed else (0, (1 << bits) - 1)
                for number in (low, 0, high):
                    self.assertEqual(values.integer({"$type": kind, "value": str(number)}, kind), number)
                for bad in (str(low - 1), str(high + 1), "-0", "+1", "01", " 1", "1.0", 1, True):
                    with self.subTest(kind=kind, bad=bad), self.assertRaises(ValueError):
                        values.logical({"$type": kind, "value": bad})

    def test_nested_records_arrays_and_lossless_scalar_tags(self):
        record = {"sequence": [{"$type": "bytes", "hex": "00ff"}, {"$type": "null"},
                    {"$type": "text", "value": "日本語\u0000é"}, {"$type": "bool", "value": False}],
                  "bits": [{"$type": "f64", "bits": bits} for bits in
                           ("0000000000000000", "8000000000000000", "7ff8000000000001")],
                  "target": {"$type": "ref", "space": "unit", "id": "unit/0/77",
                             "slot": {"$type": "u16", "value": "0"},
                             "token": {"$type": "u32", "value": "77"}}}
        values.logical(record)
        for bad in (1, True, "untyped", None, {"$type": "bytes", "hex": "FF"},
                    {"$type": "bytes", "hex": "f"}, {"$type": "null", "value": None},
                    {"$type": "bool", "value": 1}, {"$type": "text", "value": "\ud800"},
                    {"$type": "f64", "bits": "0"}, {"$type": "unknown", "value": "x"},
                    {"$type": "ref", "space": "unit", "id": "u", "slot": 1}):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                values.logical(bad)

    def test_blob_views_are_bounded_and_unavailable_is_never_an_input(self):
        view = {"$type": "blob", "id": "fixture", "offset": "4", "length": "0"}
        values.logical(view, {"fixture": 4})
        for change in ({"length": "1"}, {"offset": "5"}, {"id": "missing"}, {"offset": "01"}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                values.logical({**view, **change}, {"fixture": 4})
        unavailable = {"$type": "unavailable", "reason": "original observation missing", "evidenceId": "capture/1"}
        with self.assertRaises(ValueError):
            values.logical(unavailable)
        values.logical(unavailable, allow_unavailable=True)
        nested = {}
        for _ in range(130):
            nested = {"child": nested}
        with self.assertRaisesRegex(ValueError, "nesting"):
            values.logical(nested)


if __name__ == "__main__":
    unittest.main()
