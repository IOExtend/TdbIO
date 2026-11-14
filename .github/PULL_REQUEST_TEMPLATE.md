## What changes and why

## How it was checked

- [ ] `dotnet build TdbIO.slnx -c Release` has no warnings
- [ ] `dotnet test TdbIO.slnx -c Release` passes
- [ ] Output of `tdbio info`, `tdbio tables` and `tdbio text` on the same files is identical before and after, or the differences are listed and intended
- [ ] No Tekla models, customer data or vendor files are added
