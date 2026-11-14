using Xunit;

namespace TdbIO.Tests;

public class TableRowSemanticsTests
{
    [Fact]
    public void PropertiesExposeTimeIdsAndMarkerStatus()
    {
        var activeRow = new TableRow(100, 0x04, new byte[] { 1, 2, 3, 4 }, 5001, 5001);
        Assert.Equal(100, activeRow.Offset);
        Assert.Equal(0x04, activeRow.Marker);
        Assert.Equal(5001, activeRow.A);
        Assert.Equal(5001, activeRow.B);
        Assert.Equal(5001, activeRow.CreationTimeId);
        Assert.Equal(5001, activeRow.DeletionTimeId);
        Assert.True(activeRow.IsActive);
        Assert.False(activeRow.IsDeleted);

        var deletedRow = new TableRow(200, 0x0C, new byte[] { 1, 2, 3, 4 }, 5001, 5099);
        Assert.Equal(200, deletedRow.Offset);
        Assert.Equal(0x0C, deletedRow.Marker);
        Assert.Equal(5001, deletedRow.A);
        Assert.Equal(5099, deletedRow.B);
        Assert.Equal(5001, deletedRow.CreationTimeId);
        Assert.Equal(5099, deletedRow.DeletionTimeId);
        Assert.False(deletedRow.IsActive);
        Assert.True(deletedRow.IsDeleted);

        var dbvRow = new TableRow(300, 0x01, new byte[] { 10, 20 }, 10, 20);
        Assert.False(dbvRow.IsActive);
        Assert.False(dbvRow.IsDeleted);
        Assert.Equal(10, dbvRow.CreationTimeId);
        Assert.Equal(20, dbvRow.DeletionTimeId);
    }

    [Fact]
    public void RecordDeconstructionAndEqualityArePreserved()
    {
        var row1 = new TableRow(50, 0x04, new byte[] { 0xAA }, 100, 200);
        var (offset, marker, body, a, b) = row1;

        Assert.Equal(50, offset);
        Assert.Equal(0x04, marker);
        Assert.Equal(100, a);
        Assert.Equal(200, b);
        Assert.Equal(1, body.Length);
        Assert.Equal(0xAA, body.Span[0]);

        var row2 = new TableRow(50, 0x04, row1.Body, 100, 200);
        Assert.Equal(row1, row2);
    }

    [SkippableFact]
    public void ModelDatabasesDistinguishActiveAndDeletedRows()
    {
        var files = TestFiles.FindAll(".db1").Where(f => TdbFile.DetectKind(f) == TdbFileKind.Db1).ToList();
        if (files.Count == 0) throw TestFiles.Missing("model database (.db1) files");

        int totalActive = 0;
        int totalDeleted = 0;

        foreach (var file in files)
        {
            using var stream = File.OpenRead(file);
            var doc = (Db1Document)TdbFile.Open(stream);
            var tables = doc.ReadTables();

            foreach (var table in tables)
            {
                foreach (var row in table.Rows)
                {
                    if (row.IsActive)
                    {
                        totalActive++;
                        Assert.Equal(0x04, row.Marker);
                        Assert.False(row.IsDeleted);
                    }
                    else if (row.IsDeleted)
                    {
                        totalDeleted++;
                        Assert.Equal(0x0C, row.Marker);
                        Assert.False(row.IsActive);
                        Assert.True(row.DeletionTimeId >= row.CreationTimeId);
                    }
                }
            }
        }

        Assert.True(totalActive > 0, "Expected active rows in model databases");
    }
}
