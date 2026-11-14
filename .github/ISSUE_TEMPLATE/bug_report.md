---
name: Bug report
about: Something is read wrongly, or the tool fails
labels: bug
---

**TdbIO version** (`tdbio --version` or the package version):

**Tekla Structures version that saved the file** (and the file extension):

**Command line or API call** (remove confidential values):

**Output of `tdbio info <file>`** (if it runs):

**First 64 bytes of the file in hex** (for example `xxd -l 64 <file>` or, in PowerShell 7, `Format-Hex -Path <file> -Count 64`):

**What you expected**:

**What happened** (error messages from standard error, exit code):

Please do not attach Tekla Structures models or files with confidential content, and never a whole model. The header bytes and a short description of the file are enough to start.
