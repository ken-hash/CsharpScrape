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

        var xParts = regex.Split(x);
        var yParts = regex.Split(y);

        int minLen = Math.Min(xParts.Length, yParts.Length);
        for (int i = 0; i < minLen; i++)
        {
            if (xParts[i] != yParts[i])
            {
                if (int.TryParse(xParts[i], out int xNum) && int.TryParse(yParts[i], out int yNum))
                {
                    return xNum.CompareTo(yNum);
                }
                return string.Compare(xParts[i], yParts[i], StringComparison.OrdinalIgnoreCase);
            }
        }
        return x.Length.CompareTo(y.Length);
    }
}