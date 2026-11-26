# Stage 64 · Fields with types declared in another DLL

Run `npm run demo -- stage-64-foreign-field-types`. Calling `Run(17)`
returns `35`. The entry library constructs a cell from one DLL and a node
from another DLL. The cell stores the same node in two fields, and the
node points to itself. Both the entry and cell libraries read the node.

The CLR, Node and Edge suite also checks null field defaults, a typed
null dereference, missing or ambiguous type owners, and stable Wasm bytes
when the dependencies are reordered.
