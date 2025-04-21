using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.WinForms;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Controls;


public class Curve
{
    public string Name { get; set; }
    public List<double> Depth { get; set; }
    public List<double> Values { get; set; }

    public Curve()
    {
        Depth = new List<double>();
        Values = new List<double>();
    }
}



public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();
    }

    // Чтение данных из файла
    private List<Curve> ReadCurvesFromFile(string filePath)
    {
        var result = new List<Curve>();

        if (!File.Exists(filePath))
            return result;

        var lines = File.ReadAllLines(filePath);

        if (lines.Length < 2)
            return result;

        var header = System.Text.RegularExpressions.Regex
            .Split(lines[0], @"\s+")
            .ToList();

        var depthIndex = header.IndexOf("DEPTH");
        if (depthIndex == -1)
            throw new Exception("DEPTH столбец не найден");

        var curveNames = header.Where(h => h != "DEPTH").ToList();
        var curves = curveNames
            .Select(name => new Curve { Name = name, Depth = new List<double>(), Values = new List<double>() })
            .ToList();

        foreach (var line in lines.Skip(1)) // Пропускаем заголовок
        {
            var values = System.Text.RegularExpressions.Regex
                .Split(line.Trim(), @"\s+");

            if (values.Length != header.Count)
                continue;

            if (!double.TryParse(values[depthIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var depth))
                continue;

            for (int i = 0; i < curves.Count; i++)
            {
                var curveIndex = header.IndexOf(curves[i].Name);
                if (double.TryParse(values[curveIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
                {
                    curves[i].Depth.Add(depth);
                    curves[i].Values.Add(val);  
                }
            }
        }

        return curves;
    }

    private  List<Curve> MergeCurves(List<Curve> curves1, List<Curve> curves2)
    {
        var mergedDict = new Dictionary<string, Curve>();

        // Объединяем все кривые в одну коллекцию
        foreach (var curve in curves1.Concat(curves2))
        {
            if (!mergedDict.ContainsKey(curve.Name))
            {
                mergedDict[curve.Name] = new Curve
                {
                    Name = curve.Name,
                    Depth = new List<double>(),
                    Values = new List<double>()
                };
            }

            mergedDict[curve.Name].Depth.AddRange(curve.Depth);
            mergedDict[curve.Name].Values.AddRange(curve.Values);
        }

        // Удаляем повторы и сортируем
        foreach (var curve in mergedDict.Values)
        {
            var combined = curve.Depth.Zip(curve.Values, (d, v) => new { Depth = d, Value = v })
                                      .GroupBy(x => x.Depth)
                                      .Select(g => g.First())
                                      .OrderBy(x => x.Depth)
                                      .ToList();

            curve.Depth = combined.Select(x => x.Depth).ToList();
            curve.Values = combined.Select(x => x.Value).ToList();
        }

        return mergedDict.Values.ToList();
    }

    private LiveCharts.WinForms.CartesianChart CreateChartFromCurves(List<Curve> curves)
    {
        var cartesianChart = new LiveCharts.WinForms.CartesianChart
        {
            Dock = DockStyle.Fill
        };

        var seriesCollection = new SeriesCollection();
        var colors = new List<System.Windows.Media.Color>
    {
        System.Windows.Media.Colors.Red,
        System.Windows.Media.Colors.Blue,
        System.Windows.Media.Colors.Green,
        System.Windows.Media.Colors.Purple,
        System.Windows.Media.Colors.Orange
    };

        for (int i = 0; i < curves.Count; i++)
        {
            var curve = curves[i];
            var lineSeries = new LiveCharts.Wpf.LineSeries
            {
                Title = curve.Name,
                Values = new ChartValues<double>(curve.Values),
                PointGeometrySize = 4,
                Stroke = new System.Windows.Media.SolidColorBrush(colors[i % colors.Count]),
                Fill = System.Windows.Media.Brushes.Transparent
            };

            seriesCollection.Add(lineSeries);
        }

        cartesianChart.Series = seriesCollection;

        if (curves.Count > 0)
        {
            cartesianChart.AxisX.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Глубина",
                Labels = curves[0].Depth.ConvertAll(d => d.ToString()).ToArray()
            });
        }

        cartesianChart.AxisY.Add(new LiveCharts.Wpf.Axis
        {
            Title = "Значение",
            LabelFormatter = value => value.ToString("N")
        });

        return cartesianChart;
    }

    private void PlotCurvesClustered(List<Curve> curves)
    {
        // Удаляем старые элементы управления
        Controls.Clear();

        // Создаем таблицу для размещения двух графиков
        var tableLayout = new TableLayoutPanel
        {
            RowCount = 1,
            ColumnCount = 2,
            Dock = DockStyle.Fill
        };

        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Списки для кластеров
        var leftCurves = new List<Curve>();  // для значений с максимальной степенью от 1 до 3
        var rightCurves = new List<Curve>(); // для значений с максимальной степенью меньше 1

        foreach (var curve in curves)
        {
            // Находим максимальное значение в текущей кривой
            double maxValue = curve.Values.Max();

            // Проверяем степень максимального значения
            double degree = Math.Log10(maxValue);

            // Если степень максимального числа от 1 до 3, то на один график
            if (degree >= 1 && degree <= 3)
            {
                leftCurves.Add(curve);
            }
            // Если степень максимального числа меньше 1, то на другой график
            else
            {
                rightCurves.Add(curve);
            }
        }

        // Создаем графики для кластеров
        var chart1 = CreateChartFromCurves(leftCurves);
        var chart2 = CreateChartFromCurves(rightCurves);

        // Добавляем графики в таблицу
        tableLayout.Controls.Add(chart1, 0, 0);
        tableLayout.Controls.Add(chart2, 1, 0);

        // Добавляем таблицу в форму
        Controls.Add(tableLayout);
    }







    private void Form1_Load(object sender, EventArgs e)
    {
        var curves1 = ReadCurvesFromFile("data1.txt");
        var curves2 = ReadCurvesFromFile("data2.txt");
        var curve_final = MergeCurves(curves1, curves2);
        PlotCurvesClustered(curve_final);
    }
}
