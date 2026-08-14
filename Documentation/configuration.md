# Configuration

Synopsis works without configuration. Put `synopsis.json` at the analyzed root when repository vocabulary or
source linking needs help:

```json
{
  "title": "Ada — how it behaves",
  "description": "The promises Ada makes to candidates, customers, and consultants.",
  "sourceUrl": "https://github.com/Hive-Consulting-Community/Ada",
  "skipSegments": ["Application"],
  "exclude": ["Generated", "Snapshots"]
}
```

| Setting | Meaning |
| --- | --- |
| `title` | Cover title; defaults to `<folder> Synopsis`. |
| `description` | One-sentence orientation for the reader. |
| `sourceUrl` | GitHub repository URL. Inferred from `origin` when possible. |
| `skipSegments` | Folder names ignored when inferring modules and features. |
| `exclude` | Additional directory names not traversed. |

Command-line values override the file. Built-in exclusions cover `.git`, `bin`, `obj`, `node_modules`, build
artifacts, and test results.
