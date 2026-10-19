# Utilities Open CAD Converter

This directory contains the open-source native worker used by the Utilities
backend to convert DWG/DXF-family drawings into binary glTF 2.0 (`.glb`). It
uses OpenCADCodec for native DWG/DXF and ACIS SAT/SAB decoding and
OpenCADKernel for B-rep lifting and tessellation. No Autodesk or HOOPS runtime
is required.

## Build

Install a current stable Rust toolchain, then run:

```text
cargo build --release --locked
```

The executable is `target/release/utilities-cad-converter` (with `.exe` on
Windows). `Cargo.lock` pins the full source revisions and all transitive crates.
`dotnet build` does not rebuild this executable. After changing Rust source,
run the Cargo command above before starting the API locally. For the container
deployment, rebuild the application image with:

```text
docker compose up --build -d app
```

## Command-line contract

```text
utilities-cad-converter \
  --input <drawing.dwg> \
  --output <unique-output.glb> \
  --format <dwg|dxf|dws|dwt> \
  --linear-deflection-meters <number> \
  --angular-deflection-degrees <number> \
  [--include-hidden] \
  [--export-edge-wires] \
  [--require-complete-geometry] \
  [--max-input-bytes <integer>] \
  [--max-entities <integer>] \
  [--max-vertices <integer>] \
  [--max-triangles <integer>] \
  [--max-line-segments <integer>] \
  [--max-output-bytes <integer>]
```

The output path must not already exist. Progress is emitted as newline-delimited
JSON on stdout with a numeric `progress` property. Warnings and failures go to
stderr. Exit codes are:

- `0`: success
- `2`: invalid input or arguments
- `3`: reserved for runtime initialization/unavailability
- `4`: DWG/DXF import failure
- `5`: tessellation, completeness, or GLB export failure
- `6`: unexpected internal failure
- `7`: deterministic resource limit exceeded

The linear tolerance is supplied in metres and converted to drawing units via
`$INSUNITS`. Unitless or unknown drawings currently assume one metre per
drawing unit and emit a warning.

## Resource limits

The worker enforces deterministic limits independently of strict geometry
mode. Defaults are 256 MiB of input, 250,000 entities/instances/nodes,
5,000,000 exported `POSITION` elements, 2,000,000 source triangles, 2,000,000
line segments, and a 256 MiB GLB. The entity limit independently caps document
entities, unique compilation visits, stored prototype placements, logical
block occurrences, and emitted scene nodes. Repeated block geometry is charged
once per compiled appearance/tessellation variant rather than once per
occurrence. MINSERT fanout is checked before prototype compilation and before
placement allocation. Native and ACIS geometry are charged before copying into
the scene, and the binary chunk, JSON chunk, and complete GLB size are checked
before the output file is created. The host passes the same limits from
`FileConversion:CadMax*` configuration and repeats the input/output byte checks
at the process boundary.

## Geometry coverage

The worker starts from model space and compiles each referenced block variant
once. Every INSERT/MINSERT occurrence becomes a glTF node that references the
shared mesh and carries its translation, rotation, orientation, mirror, and
non-uniform scale; nested transforms are composed without baking duplicate
vertices. Separate variants are retained when inherited layer/color/material
state, available nesting depth, or scale-dependent tessellation differs.
Non-repeated geometry is combined into target-sized mesh batches while keeping
layer/color primitive groups intact. One exceptionally large source entity is
kept intact and remains bounded by the global geometry/output limits. A block
record's `explodable=false` setting only disables AutoCAD's interactive EXPLODE
command; it does not hide the INSERT, so the worker still renders it.

Supported output includes:

- all ACIS-backed `3DSOLID`, `REGION`, `BODY`, and `SURFACE` entities whose
  SAT/SAB records OpenCADKernel can lift;
- per-face ACIS colors/material bindings where present;
- B-rep triangles plus every non-seam drawing edge when edge export is enabled;
- lines, arcs, circles, ellipses, lightweight/heavy/3D polylines (including
  bulge arcs), `3DFACE`, `SOLID`/`TRACE`, SubD mesh, polyface mesh, and polygon
  mesh geometry;
- rational/control-point and fit-point `SPLINE` entities, plus stored or
  parametrically reconstructed `HELIX` curves, sampled to both the angular and
  chord tolerances;
- `POINT` markers and finite visualizations of `RAY`/`XLINE` reference
  geometry. Infinite entities use the model-space `$EXTMIN`/`$EXTMAX`
  diagonal (or a documented tolerance-scaled fallback) as their display span;
- line-only drawings as valid glTF `LINES` primitives.

Coordinates remain `f64` until export. Each unique mesh is rebased around a
local anchor before `f32` conversion, while node translations preserve that
anchor and subtract the global transformed bounding-box center. DWG Z-up
coordinates are then rotated to glTF Y-up with `(x, z, -y)`. This preserves
small geometry in large-coordinate drawings without duplicating shared meshes.
Source origin and unit metadata are retained in `asset.extras`.

The final JSON reports rendered occurrence totals as `triangleCount` and
`lineSegmentCount`, plus the once-encoded shared-geometry totals as
`uniqueTriangleCount` and `uniqueLineSegmentCount`.

With `--require-complete-geometry`, renderable/reference/B-rep completeness is
required: conversion fails for an unparsed ACIS
payload, ACIS lift loss/broken topology, an untessellated B-rep face, a genuine
unresolved external reference, a missing block definition, a cyclic/deep block
reference, a missing child handle (including a partially readable block), a
nonempty block that unexpectedly expands to nothing, a malformed native mesh
face, or another loss of drawable geometry identified before GLB mapping.

## Current limitations

- Text/attributes/dimensions, HATCH fills, raster/OLE/underlay content, and
  unknown/proxy entities are not converted. Counts are reported in the final
  JSON and as a stderr warning; these intentional omissions do not fail the
  ACIS completeness gate.
- glTF cannot encode mathematically infinite primitives. `RAY` and `XLINE`
  entities are therefore bounded only for visualization; `POINT` entities use
  a small three-axis wire marker because portable glTF line width and CAD
  point-display modes are not available.
- External XREF files are not loaded. A referenced, flagged XREF without local
  geometry fails strict conversion. Empty vertical-product metadata blocks that
  only carry an `xref_path` string but no XREF flag are reported as placeholders.
- Texture images, procedural materials, CAD line widths, and linetype dash
  patterns are not represented. Core glTF has no portable line-width control.
- Malformed native mesh faces are omitted and reported, and fail strict mode.
  Source faces/triangles with exactly zero drawable area are omitted and
  reported but do not fail strict mode. Triangles that collapse to zero output
  area during required `f64` to `f32` GLB mapping are likewise omitted, counted
  separately, and warned about; they do not contribute drawable output area and
  therefore do not fail the visible-geometry completeness gate.
- The host runs the worker out of process and enforces a timeout. Deterministic
  entity/geometry/output limits are not an operating-system RSS or CPU quota.
  In particular, the upstream ACIS tessellator must materialize one body's
  tessellation before the worker can inspect and charge its counts; the result
  is charged before it is copied into the scene. The worker does not claim
  containment against a native crash or out-of-memory termination.

See `THIRD_PARTY_NOTICES.md` for the pinned MPL-2.0 dependencies and source
availability. `THIRD_PARTY_LICENSES.txt` contains the package/version/source
inventory and license texts for the complete resolved Rust dependency graph.
