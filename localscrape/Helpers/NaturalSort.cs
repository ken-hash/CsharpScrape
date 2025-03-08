using System.Text.RegularExpressions;

public class NaturalSortComparer : IComparer<string?>
{
    public int Compare(string? x, string? y)
    {
        return NaturalSort(x??string.Empty, y??string.Empty);
    }

    private int NaturalSort(string x, string y)
    {
        var regex = new Regex(@"\d+");

        if (x == null || y == null) return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);

        bool xHasNumber = regex.IsMatch(x);
        bool yHasNumber = regex.IsMatch(y);
        if (!xHasNumber && yHasNumber) return -1;
        if (xHasNumber && !yHasNumber) return 1;

        var xMatches = regex.Matches(x);
        var yMatches = regex.Matches(y);

        int xIndex = 0, yIndex = 0;
        int xPos = 0, yPos = 0;

        while (xIndex < x.Length && yIndex < y.Length)
        {
            if (char.IsDigit(x[xIndex]) && char.IsDigit(y[yIndex]))
            {
                var xNumStr = xMatches[xPos++].Value;
                var yNumStr = yMatches[yPos++].Value;

                int xNum = int.Parse(xNumStr);
                int yNum = int.Parse(yNumStr);

                int result = xNum.CompareTo(yNum);
                if (result != 0) return result;

                xIndex += xNumStr.Length;
                yIndex += yNumStr.Length;
            }
            else
            {
                int result = x[xIndex].CompareTo(y[yIndex]);
                if (result != 0) return result;

                xIndex++;
                yIndex++;
            }
        }

        return x.Length.CompareTo(y.Length);
    }
}