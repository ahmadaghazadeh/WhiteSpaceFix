# WhiteSpaceFix

Console tool that fixes whitespace and line-ending noise in **current git changes** without touching real code edits.

Use it when a diff tool shows whole files as changed, but only a few lines were intentionally modified — for example after an editor normalizes LF/CRLF or reformats indentation.

## Requirements

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or later
- `git` available on `PATH`

## Quick start

Clone and build:

```bash
git clone https://github.com/ahmadaghazadeh/WhiteSpaceFix.git
cd WhiteSpaceFix
dotnet build
```

Run the interactive menu (no arguments):

```bash
dotnet run
```

Or run against a repository directly:

```bash
dotnet run -- "C:\path\to\your\repo" --dry-run
```

Publish a standalone executable:

```bash
dotnet publish -c Release -o ./out-build
./out-build/WhiteSpaceFix.exe "C:\path\to\your\repo"
```

## Usage

### CheckWhiteSpace command

One-step check: preview, apply fixes if needed, then verify diff scope:

```bash
dotnet run -- CheckWhiteSpace "C:\path\to\your\repo"
```

Branch-wide squash-equivalent check (dry-run only, no commits changed):

```bash
dotnet run -- CheckWhiteSpace "C:\path\to\your\repo" --branch --dry-run-only
```

With a specific upstream:

```bash
dotnet run -- CheckWhiteSpace "C:\path\to\your\repo" --branch --upstream origin/main --dry-run-only
```

Options:

| Option | Description |
|--------|-------------|
| `CheckWhiteSpace [repo-path]` | Preview, apply when needed, verify `git diff` vs `git diff -w`. |
| `--dry-run-only` | Preview only; do not apply fixes. |
| `--branch` | Check full branch diff (`merge-base..HEAD`), like a squashed commit. |
| `--upstream <ref>` | Upstream ref for `--branch`. Default: `origin/HEAD`, `origin/main`, or `origin/master`. |
| `--stash` | Create stash backup before apply (default). |
| `--no-stash` | Skip stash backup. |
| `--base <ref>` | Git ref to preserve whitespace from. Default: `HEAD`. |

### Interactive menu (recommended)

Run without arguments to open the menu:

```bash
dotnet run
```

Or run the built executable with no arguments:

```bash
./bin/Debug/net6.0/WhiteSpaceFix.exe
```

Menu options:

| Option | Action |
|--------|--------|
| **1. Set repository path** | Save repo path for the session |
| **2. Apply fixes** | Write whitespace fixes to disk |
| **3. Preview changes (dry-run)** | Preview fixes without writing files |
| **4. Set base ref** | Change git base ref (default: `HEAD`) |
| **5. Toggle stash before apply** | Backup current changes to `git stash` before applying fixes |
| **6. CheckWhiteSpace** | Preview, apply fixes if needed, verify diff scope |
| **7. Check branch whitespace** | Squash-equivalent dry-run (`merge-base..HEAD`, no writes) |
| **8. Set upstream ref** | Target branch for option 7 (default: auto-detect) |
| **9. Help** | Show command-line help |
| **0. Exit** | Close the tool |

Typical flow:

1. Run the tool (no arguments).
2. Choose **1** and paste your repo path.
3. Choose **3** to preview with dry-run.
4. Optional: choose **5** to turn on stash backup before apply.
5. If the summary looks right, choose **2** to apply.

### Command line

Pass the target repository path as the first argument:

```bash
dotnet run -- "C:\path\to\your\repo"
```

Or run the built executable:

```bash
./bin/Debug/net6.0/WhiteSpaceFix.exe "C:\path\to\your\repo"
```

### What it prints first

For the repo you pass in, the tool shows:

- Repository path
- Current branch
- Git status summary
- Unstaged / staged / total changed file counts
- Lists of unstaged and staged files

Then it checks only those changed files and restores whitespace from the base ref (default: `HEAD`).

### Options

| Option | Description |
|--------|-------------|
| `<repo-path>` | Path to the git repository to inspect and fix. |
| `--repo <path>` | Same as the positional `<repo-path>`. |
| `--base <ref>` | Git ref to preserve whitespace from. Default: `HEAD`. |
| `--dry-run` | Report what would be fixed without writing files. |
| `--stash` | Create a git stash backup before applying fixes. |
| `--help` | Show help. |

### Examples

Preview only:

```bash
dotnet run -- "C:\path\to\your\repo" --dry-run
```

Use a different base ref:

```bash
dotnet run -- "C:\path\to\your\repo" --base origin/master
```

## How it works

1. Finds the git repository from the path you provide.
2. Reads changed files from the current working tree:
   - unstaged changes vs `--base`
   - staged changes vs `--base`
3. For each changed file:
   - loads the version from `--base`
   - aligns lines between base and working copy
   - if line **content** is the same but whitespace/line endings differ, restores the exact bytes from `--base`
   - if line **content** changed, keeps your working copy and only normalizes new lines to the base file's dominant line-ending style
4. Skips binary files.

## Output

Per file:

- `ok` — no whitespace fix needed
- `fixed` — whitespace-only lines were restored
- `skip` — deleted, missing in base ref, or binary

Example summary:

```text
Summary: 1 fixed, 18 unchanged, 0 skipped, 119 line(s) restored.
```

## Notes

- The tool only processes files that are currently changed in git.
- It does not commit changes.
- It is intended to clean noisy diffs while preserving intentional edits on the current branch.
- Run with `--dry-run` first if you want to review before writing files.
- When **stash before apply** is enabled, the tool uses `git stash create` + `git stash store` so your working tree stays unchanged while a backup is saved to the stash list.

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).
