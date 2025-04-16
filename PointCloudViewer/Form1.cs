/**
 * 点云查看器主窗体
 * 
 * 功能：
 * 1. 支持多种格式点云文件的加载和显示（PLY、PCD、TXT、XYZ）
 * 2. 提供3D视图控制（旋转、缩放）
 * 3. 支持坐标轴和网格显示
 * 4. 点云渲染优化和深度显示
 * 
 * @author Ning
 * @date 2025-04-16
 */

using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace winform_demo;

/// <summary>
/// 点云查看器主窗体类
/// </summary>
public partial class Form1 : Form
{
    // 点云数据存储
    private float[,]? points;              // 点云坐标数据
    private float rotationX = 0;           // X轴旋转角度
    private float rotationY = 0;           // Y轴旋转角度
    private Point lastMousePos;            // 上一次鼠标位置
    private bool isMouseDown = false;      // 鼠标按下状态
    private float scale = 1.0f;            // 缩放比例
    private float pointSize = 2;           // 点大小
    private bool showAxes = true;          // 是否显示坐标轴
    private bool showGrid = true;          // 是否显示网格
    private Color pointColor = Color.White;
    private string currentFileName = "";    // 当前文件名
    private float[]? pointColors; // 用于存储点的颜色信息
    private float minX, maxX, minY, maxY, minZ, maxZ;  // 点云边界值
    private const float FOV = 60.0f;               // 视场角
    private const float NEAR_PLANE = 0.1f;        // 近裁剪面
    private const float FAR_PLANE = 1000.0f;      // 远裁剪面
    private ToolStripComboBox fileComboBox;  // 文件选择下拉框

    /// <summary>
    /// 构造函数
    /// </summary>
    public Form1()
    {
        InitializeComponent();
        SetupCustomComponents();
        this.DoubleBuffered = true;
        LoadAvailablePlyFiles();

        // 应用程序启动时自动加载点云文件
        string defaultPlyFile = "cloud_normal_smooth_0.ply";
        if (File.Exists(defaultPlyFile))
        {
            LoadPointCloud(defaultPlyFile);
        }
    }

    /// <summary>
    /// 初始化自定义组件
    /// </summary>
    private void SetupCustomComponents()
    {
        this.Text = "点云查看器";
        this.Size = new System.Drawing.Size(1024, 768);

        // 创建工具栏
        ToolStrip toolStrip = new ToolStrip();

        // 添加导入按钮
        ToolStripButton importButton = new ToolStripButton("导入文件");
        importButton.Click += ImportButton_Click;
        toolStrip.Items.Add(importButton);

        // 添加文件选择下拉框
        fileComboBox = new ToolStripComboBox();
        fileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        fileComboBox.Width = 200;
        fileComboBox.ToolTipText = "选择要显示的点云文件";
        fileComboBox.SelectedIndexChanged += FileComboBox_SelectedIndexChanged;
        toolStrip.Items.Add(new ToolStripLabel("文件："));
        toolStrip.Items.Add(fileComboBox);

        // 添加刷新按钮
        ToolStripButton refreshButton = new ToolStripButton("刷新文件列表");
        refreshButton.Click += (s, e) => LoadAvailablePlyFiles();
        toolStrip.Items.Add(refreshButton);

        toolStrip.Items.Add(new ToolStripSeparator());

        // 添加视图控制按钮
        ToolStripButton resetViewButton = new ToolStripButton("重置视图");
        resetViewButton.Click += (s, e) => { rotationX = 0; rotationY = 0; scale = 1.0f; Invalidate(); };
        toolStrip.Items.Add(resetViewButton);

        // 添加坐标轴切换按钮
        ToolStripButton toggleAxesButton = new ToolStripButton("坐标轴");
        toggleAxesButton.CheckOnClick = true;
        toggleAxesButton.Checked = showAxes;
        toggleAxesButton.Click += (s, e) => { showAxes = toggleAxesButton.Checked; Invalidate(); };
        toolStrip.Items.Add(toggleAxesButton);

        // 添加网格切换按钮
        ToolStripButton toggleGridButton = new ToolStripButton("网格");
        toggleGridButton.CheckOnClick = true;
        toggleGridButton.Checked = showGrid;
        toggleGridButton.Click += (s, e) => { showGrid = toggleGridButton.Checked; Invalidate(); };
        toolStrip.Items.Add(toggleGridButton);

        // 添加点大小控制
        ToolStripLabel pointSizeLabel = new ToolStripLabel("点大小:");
        toolStrip.Items.Add(pointSizeLabel);

        ToolStripTrackBar pointSizeTrackBar = new ToolStripTrackBar();
        pointSizeTrackBar.Minimum = 1;
        pointSizeTrackBar.Maximum = 10;
        pointSizeTrackBar.Value = (int)pointSize;
        pointSizeTrackBar.ValueChanged += (s, e) => { pointSize = pointSizeTrackBar.Value; Invalidate(); };
        toolStrip.Items.Add(pointSizeTrackBar);

        // 创建状态栏
        StatusStrip statusStrip = new StatusStrip();
        ToolStripStatusLabel fileLabel = new ToolStripStatusLabel();
        ToolStripStatusLabel pointCountLabel = new ToolStripStatusLabel();
        ToolStripStatusLabel viewInfoLabel = new ToolStripStatusLabel();
        statusStrip.Items.AddRange(new ToolStripItem[] { fileLabel, pointCountLabel, viewInfoLabel });

        // 添加控件到窗体
        this.Controls.Add(toolStrip);
        this.Controls.Add(statusStrip);

        // 设置布局
        toolStrip.Dock = DockStyle.Top;
        statusStrip.Dock = DockStyle.Bottom;

        // 添加鼠标事件处理
        this.MouseDown += Form1_MouseDown;
        this.MouseMove += Form1_MouseMove;
        this.MouseUp += Form1_MouseUp;
        this.MouseWheel += Form1_MouseWheel;

        // 更新状态栏信息
        void UpdateStatusBar()
        {
            fileLabel.Text = $"文件: {currentFileName}";
            pointCountLabel.Text = points != null ? $"点数: {points.GetLength(0)}" : "点数: 0";
            viewInfoLabel.Text = $"缩放: {scale:F2} 旋转X: {rotationX:F1}° Y: {rotationY:F1}°";
        }

        // 定期更新状态栏
        System.Windows.Forms.Timer statusUpdateTimer = new System.Windows.Forms.Timer();
        statusUpdateTimer.Interval = 100;
        statusUpdateTimer.Tick += (s, e) => UpdateStatusBar();
        statusUpdateTimer.Start();
    }

    /// <summary>
    /// 导入文件按钮点击事件处理
    /// </summary>
    private void ImportButton_Click(object? sender, EventArgs e)
    {
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "点云文件|*.ply;*.pcd;*.txt;*.xyz|PLY文件|*.ply|PCD文件|*.pcd|文本文件|*.txt|XYZ文件|*.xyz|所有文件|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.Title = "选择点云文件";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string targetPath = Path.Combine(Application.StartupPath, Path.GetFileName(openFileDialog.FileName));
                    if (File.Exists(targetPath))
                    {
                        if (MessageBox.Show("文件已存在，是否覆盖？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        {
                            return;
                        }
                    }
                    File.Copy(openFileDialog.FileName, targetPath, true);
                    LoadAvailablePlyFiles();

                    // 选择新导入的文件
                    fileComboBox.SelectedItem = Path.GetFileName(targetPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入文件时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    /// <summary>
    /// 加载可用的点云文件列表
    /// </summary>
    private void LoadAvailablePlyFiles()
    {
        try
        {
            // 支持的文件扩展名
            string[] extensions = new[] { "*.ply", "*.pcd", "*.txt", "*.xyz" };
            var files = new List<string>();

            // 获取应用程序目录中的所有支持的文件
            foreach (string ext in extensions)
            {
                files.AddRange(Directory.GetFiles(Application.StartupPath, ext)
                    .Select(Path.GetFileName)
                    .Where(f => f != null));
            }

            // 获取上级目录中的文件
            string parentDir = Directory.GetParent(Application.StartupPath)?.FullName ?? "";
            if (Directory.Exists(parentDir))
            {
                foreach (string ext in extensions)
                {
                    files.AddRange(Directory.GetFiles(parentDir, ext)
                        .Select(Path.GetFileName)
                        .Where(f => f != null));
                }
            }

            // 更新下拉框
            fileComboBox.Items.Clear();
            if (files.Any())
            {
                fileComboBox.Items.AddRange(files.Distinct().ToArray());
                fileComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载文件列表时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 文件选择改变事件处理
    /// </summary>
    private void FileComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (fileComboBox.SelectedItem is string fileName)
        {
            // 首先在应用程序目录中查找
            string filePath = Path.Combine(Application.StartupPath, fileName);
            if (!File.Exists(filePath))
            {
                // 如果在应用程序目录中没有找到，则在上级目录中查找
                string? parentDir = Directory.GetParent(Application.StartupPath)?.FullName;
                if (parentDir != null)
                {
                    filePath = Path.Combine(parentDir, fileName);
                }
            }

            if (File.Exists(filePath))
            {
                currentFileName = fileName;
                LoadPointCloud(filePath);
            }
            else
            {
                MessageBox.Show($"找不到文件：{fileName}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>
    /// 鼠标按下事件处理
    /// </summary>
    private void Form1_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isMouseDown = true;
            lastMousePos = e.Location;
        }
    }

    /// <summary>
    /// 鼠标移动事件处理
    /// </summary>
    private void Form1_MouseMove(object? sender, MouseEventArgs e)
    {
        if (isMouseDown)
        {
            float dx = e.X - lastMousePos.X;
            float dy = e.Y - lastMousePos.Y;

            rotationY += dx * 0.5f;
            rotationX += dy * 0.5f;

            lastMousePos = e.Location;
            this.Invalidate();
        }
    }

    /// <summary>
    /// 鼠标释放事件处理
    /// </summary>
    private void Form1_MouseUp(object? sender, MouseEventArgs e)
    {
        isMouseDown = false;
    }

    /// <summary>
    /// 鼠标滚轮事件处理
    /// </summary>
    private void Form1_MouseWheel(object? sender, MouseEventArgs e)
    {
        float zoomFactor = 1.1f;
        if (e.Delta > 0)
            scale *= zoomFactor;
        else
            scale /= zoomFactor;

        scale = Math.Max(0.1f, Math.Min(10.0f, scale));
        this.Invalidate();
    }

    /// <summary>
    /// 重写绘制方法
    /// </summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (points == null) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Color.Black);

        int width = this.ClientSize.Width;
        int height = this.ClientSize.Height;

        // 计算投影矩阵
        float aspect = (float)width / height;
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            FOV * (float)Math.PI / 180.0f,
            aspect,
            NEAR_PLANE,
            FAR_PLANE
        );

        // 计算视图矩阵
        Matrix4x4 view = Matrix4x4.CreateTranslation(0, 0, -5) *
                       Matrix4x4.CreateRotationX(rotationX * (float)Math.PI / 180.0f) *
                       Matrix4x4.CreateRotationY(rotationY * (float)Math.PI / 180.0f);

        // 计算模型矩阵
        float modelScale = 1.0f / Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
        Vector3 center = new Vector3(-(maxX + minX) / 2, -(maxY + minY) / 2, -(maxZ + minZ) / 2);
        Matrix4x4 model = Matrix4x4.CreateTranslation(center) *
                        Matrix4x4.CreateScale(modelScale * scale);

        // 组合变换矩阵
        Matrix4x4 transform = model * view * projection;

        // 绘制点云
        using (SolidBrush brush = new SolidBrush(Color.White))
        {
            for (int i = 0; i < points.GetLength(0); i++)
            {
                Vector4 point = new Vector4(
                    points[i, 0],
                    points[i, 1],
                    points[i, 2],
                    1.0f
                );

                // 应用变换
                point = Vector4.Transform(point, transform);

                // 执行透视除法
                if (point.W != 0)
                {
                    point.X /= point.W;
                    point.Y /= point.W;
                    point.Z /= point.W;
                }

                // 转换到屏幕坐标
                float screenX = (point.X + 1) * width / 2;
                float screenY = (-point.Y + 1) * height / 2;

                // 只绘制在视野内的点
                if (point.Z >= -1 && point.Z <= 1 &&
                    screenX >= 0 && screenX < width &&
                    screenY >= 0 && screenY < height)
                {
                    // 根据深度计算颜色
                    int intensity = (int)((point.Z + 1) * 127.5f);
                    brush.Color = Color.FromArgb(255, intensity, intensity, intensity);

                    // 根据深度计算点大小
                    float size = pointSize * (1.0f - point.Z * 0.5f);
                    e.Graphics.FillEllipse(brush,
                        screenX - size / 2,
                        screenY - size / 2,
                        size,
                        size);
                }
            }
        }

        // 绘制坐标轴
        if (showAxes)
        {
            DrawAxis(e.Graphics, transform, width, height);
        }

        // 绘制网格
        if (showGrid)
        {
            DrawGrid(e.Graphics, transform, width, height);
        }

        // 绘制信息
        using (Font font = new Font("Arial", 10))
        using (SolidBrush brush = new SolidBrush(Color.White))
        {
            string info = $"点数: {points.GetLength(0)} | 旋转: X={rotationX:F1}° Y={rotationY:F1}° | 缩放: {scale:F2}";
            e.Graphics.DrawString(info, font, brush, 10, 10);
        }
    }

    /// <summary>
    /// 绘制坐标轴
    /// </summary>
    private void DrawAxis(Graphics g, Matrix4x4 transform, int width, int height)
    {
        // 坐标轴长度
        float axisLength = 1.0f;

        // 原点
        Vector4 origin = Vector4.Transform(new Vector4(0, 0, 0, 1), transform);
        Vector4 xAxis = Vector4.Transform(new Vector4(axisLength, 0, 0, 1), transform);
        Vector4 yAxis = Vector4.Transform(new Vector4(0, axisLength, 0, 1), transform);
        Vector4 zAxis = Vector4.Transform(new Vector4(0, 0, axisLength, 1), transform);

        // 执行透视除法
        PerspectiveDivide(ref origin);
        PerspectiveDivide(ref xAxis);
        PerspectiveDivide(ref yAxis);
        PerspectiveDivide(ref zAxis);

        // 转换到屏幕坐标
        PointF o = ToScreen(origin, width, height);
        PointF x = ToScreen(xAxis, width, height);
        PointF y = ToScreen(yAxis, width, height);
        PointF z = ToScreen(zAxis, width, height);

        // 绘制坐标轴
        using (Pen xPen = new Pen(Color.Red, 2))
        using (Pen yPen = new Pen(Color.Green, 2))
        using (Pen zPen = new Pen(Color.Blue, 2))
        {
            g.DrawLine(xPen, o, x);
            g.DrawLine(yPen, o, y);
            g.DrawLine(zPen, o, z);
        }
    }

    /// <summary>
    /// 绘制网格
    /// </summary>
    private void DrawGrid(Graphics g, Matrix4x4 transform, int width, int height)
    {
        using (Pen gridPen = new Pen(Color.FromArgb(40, 40, 40), 1))
        {
            float gridSize = 0.2f;
            int gridCount = 5;

            for (int i = -gridCount; i <= gridCount; i++)
            {
                for (int j = -gridCount; j <= gridCount; j++)
                {
                    // XZ平面上的网格线
                    Vector4 start = new Vector4(i * gridSize, 0, j * gridSize, 1);
                    Vector4 endX = new Vector4((i + 1) * gridSize, 0, j * gridSize, 1);
                    Vector4 endZ = new Vector4(i * gridSize, 0, (j + 1) * gridSize, 1);

                    start = Vector4.Transform(start, transform);
                    endX = Vector4.Transform(endX, transform);
                    endZ = Vector4.Transform(endZ, transform);

                    PerspectiveDivide(ref start);
                    PerspectiveDivide(ref endX);
                    PerspectiveDivide(ref endZ);

                    PointF s = ToScreen(start, width, height);
                    PointF ex = ToScreen(endX, width, height);
                    PointF ez = ToScreen(endZ, width, height);

                    g.DrawLine(gridPen, s, ex);
                    g.DrawLine(gridPen, s, ez);
                }
            }
        }
    }

    /// <summary>
    /// 执行透视除法
    /// </summary>
    private void PerspectiveDivide(ref Vector4 point)
    {
        if (point.W != 0)
        {
            point.X /= point.W;
            point.Y /= point.W;
            point.Z /= point.W;
        }
    }

    /// <summary>
    /// 转换到屏幕坐标
    /// </summary>
    private PointF ToScreen(Vector4 point, int width, int height)
    {
        return new PointF(
            (point.X + 1) * width / 2,
            (-point.Y + 1) * height / 2
        );
    }

    /// <summary>
    /// 加载点云文件
    /// </summary>
    private void LoadPointCloud(string filename)
    {
        try
        {
            string extension = Path.GetExtension(filename).ToLower();
            switch (extension)
            {
                case ".ply":
                    LoadPlyFile(filename);
                    break;
                case ".pcd":
                case ".txt":
                case ".xyz":
                    LoadTextFile(filename);
                    break;
                default:
                    throw new Exception("不支持的文件格式");
            }

            this.Invalidate();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载点云时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 加载PLY格式文件
    /// </summary>
    private void LoadPlyFile(string filename)
    {
        string[] lines = File.ReadAllLines(filename);
        int vertexCount = 0;
        int currentLine = 0;

        // 解析头部
        while (currentLine < lines.Length)
        {
            string line = lines[currentLine++];
            if (line.StartsWith("element vertex"))
            {
                vertexCount = int.Parse(line.Split(' ')[2]);
            }
            else if (line == "end_header")
            {
                break;
            }
        }

        // 读取点云数据
        points = new float[vertexCount, 3];
        for (int i = 0; i < vertexCount && currentLine < lines.Length; i++)
        {
            string[] values = lines[currentLine++].Split(' ');
            points[i, 0] = float.Parse(values[0], CultureInfo.InvariantCulture);
            points[i, 1] = float.Parse(values[1], CultureInfo.InvariantCulture);
            points[i, 2] = float.Parse(values[2], CultureInfo.InvariantCulture);

            // 更新边界值
            if (i == 0)
            {
                minX = maxX = points[i, 0];
                minY = maxY = points[i, 1];
                minZ = maxZ = points[i, 2];
            }
            else
            {
                minX = Math.Min(minX, points[i, 0]);
                maxX = Math.Max(maxX, points[i, 0]);
                minY = Math.Min(minY, points[i, 1]);
                maxY = Math.Max(maxY, points[i, 1]);
                minZ = Math.Min(minZ, points[i, 2]);
                maxZ = Math.Max(maxZ, points[i, 2]);
            }
        }
    }

    /// <summary>
    /// 加载文本格式点云文件
    /// </summary>
    private void LoadTextFile(string filename)
    {
        string[] lines = File.ReadAllLines(filename);
        var pointList = new List<(float x, float y, float z)>();

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            string[] values = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length >= 3)
            {
                float x = float.Parse(values[0], CultureInfo.InvariantCulture);
                float y = float.Parse(values[1], CultureInfo.InvariantCulture);
                float z = float.Parse(values[2], CultureInfo.InvariantCulture);
                pointList.Add((x, y, z));

                // 更新边界值
                if (pointList.Count == 1)
                {
                    minX = maxX = x;
                    minY = maxY = y;
                    minZ = maxZ = z;
                }
                else
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                    minZ = Math.Min(minZ, z);
                    maxZ = Math.Max(maxZ, z);
                }
            }
        }

        points = new float[pointList.Count, 3];
        for (int i = 0; i < pointList.Count; i++)
        {
            points[i, 0] = pointList[i].x;
            points[i, 1] = pointList[i].y;
            points[i, 2] = pointList[i].z;
        }
    }
}

/// <summary>
/// 自定义工具栏TrackBar控件
/// </summary>
public class ToolStripTrackBar : ToolStripControlHost
{
    private TrackBar trackBar;

    public ToolStripTrackBar() : base(new TrackBar())
    {
        trackBar = Control as TrackBar;
        trackBar.AutoSize = false;
        trackBar.Height = 20;
        trackBar.Width = 100;
        trackBar.TickStyle = TickStyle.None;
    }

    public int Value
    {
        get { return trackBar.Value; }
        set { trackBar.Value = value; }
    }

    public int Minimum
    {
        get { return trackBar.Minimum; }
        set { trackBar.Minimum = value; }
    }

    public int Maximum
    {
        get { return trackBar.Maximum; }
        set { trackBar.Maximum = value; }
    }

    public event EventHandler ValueChanged
    {
        add { trackBar.ValueChanged += value; }
        remove { trackBar.ValueChanged -= value; }
    }
}
