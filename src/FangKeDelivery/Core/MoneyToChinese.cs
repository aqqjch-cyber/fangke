using System.Text;

namespace FangKeDelivery.Core;

/// <summary>人民币金额转中文大写。</summary>
public static class MoneyToChinese
{
    private static readonly string[] Digits = { "零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖" };
    private static readonly string[] Units = { "仟", "佰", "拾", "" };
    private static readonly string[] GroupUnits = { "", "万", "亿", "万亿" };

    public static string Convert(decimal amount)
    {
        var negative = amount < 0;
        amount = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);

        var integerPart = (long)decimal.Truncate(amount);
        var cents = (int)Math.Round((amount - integerPart) * 100);
        if (cents == 100)
        {
            integerPart += 1;
            cents = 0;
        }

        if (integerPart == 0 && cents == 0)
            return "零元整";

        var jiao = cents / 10;
        var fen = cents % 10;
        var sb = new StringBuilder();

        if (negative)
            sb.Append('负');

        if (integerPart > 0)
            sb.Append(ConvertInteger(integerPart)).Append('元');

        if (cents == 0)
        {
            sb.Append('整');
        }
        else if (jiao == 0)
        {
            if (integerPart > 0)
                sb.Append('零');
            sb.Append(Digits[fen]).Append('分');
        }
        else
        {
            sb.Append(Digits[jiao]).Append('角');
            if (fen > 0)
                sb.Append(Digits[fen]).Append('分');
        }

        return sb.ToString();
    }

    private static string ConvertInteger(long value)
    {
        var groups = new List<int>();
        for (var v = value; v > 0; v /= 10000)
            groups.Add((int)(v % 10000));

        var sb = new StringBuilder();
        var zeroPending = false;

        for (var i = groups.Count - 1; i >= 0; i--)
        {
            var group = groups[i];
            if (group == 0)
            {
                zeroPending = true;
                continue;
            }

            if (sb.Length > 0 && (zeroPending || group < 1000))
                sb.Append('零');

            sb.Append(ConvertGroup(group)).Append(GroupUnits[i]);
            zeroPending = false;
        }

        return sb.ToString();
    }

    private static string ConvertGroup(int group)
    {
        var sb = new StringBuilder();
        var digits = new[] { group / 1000, group / 100 % 10, group / 10 % 10, group % 10 };
        var zeroPending = false;
        var started = false;

        for (var i = 0; i < 4; i++)
        {
            var digit = digits[i];
            if (digit == 0)
            {
                if (started)
                    zeroPending = true;
                continue;
            }

            if (zeroPending)
            {
                sb.Append('零');
                zeroPending = false;
            }

            sb.Append(Digits[digit]).Append(Units[i]);
            started = true;
        }

        return sb.ToString();
    }
}
