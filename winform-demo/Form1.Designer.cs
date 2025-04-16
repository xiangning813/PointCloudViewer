/**
 * 点云查看器窗体设计器代码
 * 
 * 功能：
 * 1. 定义窗体的UI组件
 * 2. 初始化组件属性和布局
 * 3. 管理组件的资源释放
 * 
 * @author Ning
 * @date 2024-03-19
 */

namespace winform_demo;

partial class Form1
{
    /// <summary>
    /// 必需的设计器变量
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// 清理所有正在使用的资源
    /// </summary>
    /// <param name="disposing">如果应释放托管资源，为 true；否则为 false</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// 设计器支持所需的方法 - 不要修改
    /// 此方法的内容。
    /// </summary>
    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1024, 768);
        this.Text = "点云查看器";
    }

    #endregion
}
