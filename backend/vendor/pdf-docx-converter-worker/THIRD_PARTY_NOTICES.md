# Third-Party Notices and Licence Boundary

The Python PDF-to-DOCX worker vendors the official `pdf2docx` v0.5.13 source
tree at commit `3e1c2319d6a3fbf2ae4d46c3ab734b7fc87bd9b4` under the MIT License.
The upstream `LICENSE`, source paths, filenames, documentation, and test
fixtures are retained in this directory. Artifex announced in March 2026 that
it no longer actively maintains `pdf2docx`; the repository remains available
for community maintenance.

`pdf2docx` depends on PyMuPDF/MuPDF. PyMuPDF 1.28.2 is available under the GNU
AGPL or an Artifex commercial licence. Review the AGPL obligations before
network deployment or distribution, or obtain an applicable commercial licence
from Artifex. The full AGPL text is retained in
`LICENSE.PyMuPDF-AGPL-3.0.txt`.

`requirements.lock` pins the external Python dependency graph and hashes; it
does not install a `pdf2docx` wheel. The resolved dependency inventory and
licence identifiers are recorded in `THIRD_PARTY_LICENSES.txt`. This notice is
not legal advice.
