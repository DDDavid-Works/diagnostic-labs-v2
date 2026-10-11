---
name: push-all
description: Commit and push the current DiagnosticLabs work to origin/main when the user says "Push all" (or "Push"). Never commit or push without that instruction.
---

# "Push all"

The user decides when work is committed. Finish the task, report, and wait; do **not** commit or push on your own. Approval to push once does not carry over to later work.

When the user says "Push all":
1. **Review what is in the working tree BEFORE staging.** Run `git status --short` and `git diff --stat` and list every file. The user may have another session or editor working in the same folder: files you did not create or change in THIS task (deployment scripts, publish profiles, `.gitignore`, project files...) must not be swept in by `git add -A`. If any file is not from the task you are pushing, stop and ask, or stage only your own files by name. Once something is pushed it cannot be undone without a force-push, so never skip this check, even when "the tree was clean a minute ago". Also flag anything sensitive (secrets, scratch files, build output).
2. Run the build and `dotnet test` if you have not since the last change (a running DiagnosticLabs app locks `src/DiagnosticLabs.Wpf/bin`: build that project with `-o "$env:TEMP\outwpf"`). Do not push a red build.
3. Commit with a short subject, a few lines saying what and why, and the attribution line the session instructions give (`Co-Authored-By: ...`). Use a heredoc with the Bash tool (PowerShell here-strings failed with `git commit -F -`):
   ```bash
   git add <the files from step 1, by name or by folder>   # `git add -A` only when step 1 showed nothing but this task's files
   git commit -q -F - <<'EOF'
   Subject line

   - what changed and why

   Co-Authored-By: <the attribution line from the session instructions>
   EOF
   git push
   ```
   Related but separate pieces (for example a theme change and its small follow-up fixes) can be separate commits.
4. Report the short hash and branch (`git push` prints `old..new  main -> main`), confirm the working tree is clean, and repeat anything still open (open questions, things the user must do such as setting a new service's price).
5. Line-ending warnings ("LF will be replaced by CRLF") are harmless.

Never force-push, never skip hooks, never push to another remote or branch unless asked.
