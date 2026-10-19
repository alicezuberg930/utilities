# Third-Party Notices

The `utilities-cad-converter` worker code in this directory is provided under
the MIT license declared in `Cargo.toml`. `THIRD_PARTY_LICENSES.txt` is the
dependency-complete attribution bundle generated from the locked Cargo graph;
it contains package names, versions, source links, and the applicable license
texts for the Rust code distributed in the worker binary.

The worker links the following Mozilla Public License 2.0 components. The
MPL-2.0 applies to those components, not as a relicensing of the worker's
MIT-authored source.

## OpenCADCodec (`opencadcodec`)

- Project: https://github.com/HakanSeven12/opencadcodec
- Locked revision: `f385f75decc311b7caeefb22d4e7435d97e12edb`
- Corresponding source: https://github.com/HakanSeven12/opencadcodec/tree/f385f75decc311b7caeefb22d4e7435d97e12edb
- License: Mozilla Public License 2.0
- License text: https://github.com/HakanSeven12/opencadcodec/blob/f385f75decc311b7caeefb22d4e7435d97e12edb/LICENSE

OpenCADCodec provides the native DWG/DXF readers and the ACIS SAT/SAB decoder.
The worker consumes the upstream revision without a vendored source
modification.

## OpenCADKernel (`opencadkernel`)

- Project: https://github.com/HakanSeven12/opencadkernel
- Locked revision: `9841749422c8fb0fa1f68205ac725d27f13e13bd`
- Corresponding source: https://github.com/HakanSeven12/opencadkernel/tree/9841749422c8fb0fa1f68205ac725d27f13e13bd
- License: Mozilla Public License 2.0
- License text: https://github.com/HakanSeven12/opencadkernel/blob/9841749422c8fb0fa1f68205ac725d27f13e13bd/LICENSE

OpenCADKernel provides ACIS-to-B-rep lifting and B-rep tessellation. The worker
consumes the upstream revision without a vendored source modification.

The exact dependency graph used for a build is recorded in `Cargo.lock`.
Recipients can obtain the Corresponding Source for the MPL-covered components
from the immutable revision links above.

## Regenerating the complete license bundle

The checked-in report is generated with cargo-about 0.9.2 and the reviewed
policy in `about.toml`:

```text
cargo install cargo-about --locked --version 0.9.2 --features cli
cargo about generate about.hbs -o THIRD_PARTY_LICENSES.txt
```

Regenerate the report whenever `Cargo.lock` changes. The production container
installs this notice, the worker's MIT license, and
`THIRD_PARTY_LICENSES.txt` under `/usr/share/doc/utilities-cad-converter/`.
