/**
 * 点云查看器程序入口
 * 
 * 功能：
 * 1. 初始化应用程序
 * 2. 创建并运行主窗体
 * 
 * @author Ning
 * @date 2025-04-16
 */

namespace winform_demo;

using System.Windows.Forms;

/// <summary>
/// 程序入口类
/// </summary>
internal static class Program
{
    /// <summary>
    /// 应用程序主入口点
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}