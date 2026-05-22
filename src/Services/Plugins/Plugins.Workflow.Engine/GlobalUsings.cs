// The engine plugin consumes the full Abstractions surface — every implementation here pulls
// types from across all five sub-namespaces. Globalising avoids 5-line using blocks at the top
// of every file. Consumer projects (App.*) add the imports they need per-file.
// Root namespace gives access to plugin-wide conventions (e.g. WorkflowJsonOptions).

// Engine-internal services namespace — used by built-in actions (e.g. SetRunNameActionType
// reuses WorkflowRunner.NormalizeName) and engine-internal cross-cutting helpers.


