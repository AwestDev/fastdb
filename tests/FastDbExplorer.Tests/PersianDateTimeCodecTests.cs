using FastDbExplorer.Domain;

namespace FastDbExplorer.Tests;

public class PersianDateTimeCodecTests
{
    [Fact]
    public void Persian_selection_becomes_the_database_gregorian_value()
    {
        var value = PersianDateTimeCodec.FromPersianCalendar(1405, 3, 16, 22, 10, 0);

        Assert.Equal(new DateTime(2026, 6, 6, 22, 10, 0), value);
        Assert.Equal("2026-06-06 22:10:00", PersianDateTimeCodec.ToDatabaseValue(value));
        Assert.Equal("1405/03/16 22:10:00", PersianDateTimeCodec.ToPersianDisplay(value));
    }

    [Theory]
    [InlineData("2026-06-06 22:10:00")]
    [InlineData("2026-06-06 22:10:00.1234567")]
    public void Database_values_can_be_loaded_into_the_picker(string value)
    {
        Assert.True(PersianDateTimeCodec.TryParseDatabaseValue(value, out var parsed));
        Assert.Equal(new DateTime(2026, 6, 6, 22, 10, 0).AddTicks(value.EndsWith("1234567", StringComparison.Ordinal) ? 1_234_567 : 0), parsed);
    }

    [Fact]
    public void Persian_date_conversion_rejects_invalid_days()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PersianDateTimeCodec.FromPersianCalendar(1405, 7, 31, 0, 0, 0));
    }
}
