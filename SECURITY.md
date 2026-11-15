# Security policy

## Supported versions

TdbIO is in preview (0.x). Security fixes are made on the latest release only.

## Reporting a vulnerability

Please do not open a public issue for a security problem. Use GitHub private vulnerability reporting:
open the Security tab of the repository and choose "Report a vulnerability". Include the version of TdbIO, the
command or API call, and a description of the problem. A file that triggers it helps, but do not send files with
confidential content; a crafted or cut-down file, or the first bytes of the file in hex, is usually enough.

You can expect an acknowledgement within a few days. Fixes are released as soon as they are verified, and the report
is credited unless you prefer otherwise.

## Scope

TdbIO parses files that can come from untrusted sources. Problems that matter most:

- crashes other than the documented exceptions, hangs or unbounded memory use caused by a crafted file (for example a
  gzip file that unpacks to far more than its size suggests);
- writing any file other than the output file a command was given (the library and the command line tool never modify an input file);
- any behaviour that executes code or contacts the network (the library and the command line tool do neither).
