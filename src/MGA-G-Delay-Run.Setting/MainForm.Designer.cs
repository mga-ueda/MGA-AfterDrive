using MGA_G_Delay_Run.Controls;
using MGA_G_Delay_Run.Theme;

namespace MGA_G_Delay_Run.Setting;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        rootLayout = new TableLayoutPanel();
        optionsBar = new TableLayoutPanel();
        maxWaitRow = new FlowLayoutPanel();
        maxWaitLabel = new Label();
        maxWaitTextBox = new TextBox();
        maxWaitUnitLabel = new Label();
        startMinimizedCheckBox = new CheckBox();
        entryGrid = new DataGridView();
        delayColumn = new DataGridViewTextBoxColumn();
        fileNameColumn = new DataGridViewTextBoxColumn();
        pathColumn = new DataGridViewTextBoxColumn();
        optionColumn = new DataGridViewTextBoxColumn();
        restartColumn = new DataGridViewCheckBoxColumn();
        buttonBar = new TableLayoutPanel();
        buttonLayout = new FlowLayoutPanel();
        startAllButton = new AppButton();
        saveButton = new AppButton();
        cancelButton = new AppButton();
        gridContextMenu = new ContextMenuStrip(components);
        testRunMenuItem = new ToolStripMenuItem();
        deleteMenuItem = new ToolStripMenuItem();
        rootLayout.SuspendLayout();
        optionsBar.SuspendLayout();
        maxWaitRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)entryGrid).BeginInit();
        buttonBar.SuspendLayout();
        buttonLayout.SuspendLayout();
        gridContextMenu.SuspendLayout();
        SuspendLayout();
        //
        // rootLayout
        //
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(optionsBar, 0, 0);
        rootLayout.Controls.Add(entryGrid, 0, 1);
        rootLayout.Controls.Add(buttonBar, 0, 2);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Name = "rootLayout";
        rootLayout.RowCount = 3;
        // オプション行は内容＋上下 Spacing。ButtonHeight 前提にしない（TextBox 高さが合わない）
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, AppLayout.Spacing + AppLayout.ButtonHeight + AppLayout.Spacing));
        rootLayout.TabIndex = 0;
        //
        // optionsBar（上段: 最大待機時間 / 下段: トレイ起動。外側 Padding は Spacing）
        //
        optionsBar.AutoSize = true;
        optionsBar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        optionsBar.ColumnCount = 1;
        optionsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        optionsBar.Controls.Add(maxWaitRow, 0, 0);
        optionsBar.Controls.Add(startMinimizedCheckBox, 0, 1);
        optionsBar.Dock = DockStyle.Fill;
        optionsBar.Name = "optionsBar";
        optionsBar.Padding = new Padding(AppLayout.Spacing);
        optionsBar.RowCount = 2;
        optionsBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        optionsBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        optionsBar.TabIndex = 0;
        //
        // maxWaitRow
        //
        maxWaitRow.AutoSize = true;
        maxWaitRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        maxWaitRow.Controls.Add(maxWaitLabel);
        maxWaitRow.Controls.Add(maxWaitTextBox);
        maxWaitRow.Controls.Add(maxWaitUnitLabel);
        maxWaitRow.Dock = DockStyle.Fill;
        maxWaitRow.Margin = new Padding(0);
        maxWaitRow.Name = "maxWaitRow";
        maxWaitRow.TabIndex = 0;
        maxWaitRow.WrapContents = false;
        //
        // maxWaitLabel
        //
        maxWaitLabel.AutoSize = true;
        maxWaitLabel.Anchor = AnchorStyles.Left;
        maxWaitLabel.Margin = new Padding(0, 0, AppLayout.Spacing, 0);
        maxWaitLabel.Name = "maxWaitLabel";
        maxWaitLabel.TabIndex = 0;
        maxWaitLabel.Text = "最大待機時間";
        maxWaitLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // maxWaitTextBox（5 文字幅。実幅は OnLoad でフォント基準に再計算）
        //
        maxWaitTextBox.BorderStyle = BorderStyle.FixedSingle;
        maxWaitTextBox.Margin = new Padding(0, 0, AppLayout.Spacing, 0);
        maxWaitTextBox.MaxLength = 5;
        maxWaitTextBox.Name = "maxWaitTextBox";
        maxWaitTextBox.Size = new Size(48, 23);
        maxWaitTextBox.TabIndex = 1;
        maxWaitTextBox.Text = "180";
        maxWaitTextBox.TextAlign = HorizontalAlignment.Center;
        maxWaitTextBox.TextChanged += MaxWaitTextBox_TextChanged;
        maxWaitTextBox.KeyPress += MaxWaitTextBox_KeyPress;
        //
        // maxWaitUnitLabel
        //
        maxWaitUnitLabel.AutoSize = true;
        maxWaitUnitLabel.Anchor = AnchorStyles.Left;
        maxWaitUnitLabel.Margin = new Padding(0);
        maxWaitUnitLabel.Name = "maxWaitUnitLabel";
        maxWaitUnitLabel.TabIndex = 2;
        maxWaitUnitLabel.Text = "秒";
        maxWaitUnitLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // startMinimizedCheckBox（最大待機時間の下。行間は Spacing）
        //
        startMinimizedCheckBox.AutoSize = true;
        startMinimizedCheckBox.BackColor = Color.Transparent;
        startMinimizedCheckBox.Checked = false;
        startMinimizedCheckBox.ForeColor = AppTheme.Foreground;
        startMinimizedCheckBox.Margin = new Padding(0, AppLayout.Spacing, 0, 0);
        startMinimizedCheckBox.Name = "startMinimizedCheckBox";
        startMinimizedCheckBox.TabIndex = 3;
        startMinimizedCheckBox.Text = "タスクトレイに最小化して起動";
        startMinimizedCheckBox.UseVisualStyleBackColor = false;
        startMinimizedCheckBox.CheckedChanged += StartMinimizedCheckBox_CheckedChanged;
        //
        // entryGrid
        //
        entryGrid.AllowDrop = true;
        entryGrid.AllowUserToAddRows = false;
        entryGrid.AllowUserToDeleteRows = false;
        entryGrid.AllowUserToResizeColumns = false;
        entryGrid.AllowUserToResizeRows = false;
        entryGrid.AutoGenerateColumns = false;
        entryGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        entryGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        entryGrid.Columns.AddRange(delayColumn, fileNameColumn, pathColumn, optionColumn, restartColumn);
        entryGrid.ContextMenuStrip = gridContextMenu;
        entryGrid.Dock = DockStyle.Fill;
        entryGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        entryGrid.MultiSelect = true;
        entryGrid.Name = "entryGrid";
        entryGrid.RowHeadersVisible = false;
        entryGrid.ScrollBars = ScrollBars.None;
        entryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        entryGrid.TabIndex = 0;
        entryGrid.DragDrop += EntryGrid_DragDrop;
        entryGrid.DragEnter += EntryGrid_DragEnter;
        entryGrid.CellEndEdit += EntryGrid_CellEndEdit;
        entryGrid.CurrentCellDirtyStateChanged += EntryGrid_CurrentCellDirtyStateChanged;
        entryGrid.DataError += EntryGrid_DataError;
        //
        // delayColumn
        //
        delayColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        delayColumn.DataPropertyName = "Delay";
        delayColumn.DefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleCenter,
        };
        delayColumn.HeaderText = "Delay";
        delayColumn.MinimumWidth = 40;
        delayColumn.Name = "delayColumn";
        delayColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
        //
        // fileNameColumn
        //
        fileNameColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        fileNameColumn.DataPropertyName = "FileName";
        fileNameColumn.HeaderText = "File Name";
        fileNameColumn.MinimumWidth = 40;
        fileNameColumn.Name = "fileNameColumn";
        fileNameColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
        //
        // pathColumn
        //
        pathColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        pathColumn.DataPropertyName = "Path";
        pathColumn.HeaderText = "Path";
        pathColumn.MinimumWidth = 40;
        pathColumn.Name = "pathColumn";
        pathColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
        //
        // optionColumn
        //
        optionColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        optionColumn.DataPropertyName = "Option";
        optionColumn.HeaderText = "Option";
        optionColumn.MinimumWidth = 40;
        optionColumn.Name = "optionColumn";
        optionColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
        //
        // restartColumn
        //
        restartColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        restartColumn.DataPropertyName = "Restart";
        restartColumn.FalseValue = false;
        restartColumn.HeaderText = "Restart";
        restartColumn.MinimumWidth = 40;
        restartColumn.Name = "restartColumn";
        restartColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
        restartColumn.ThreeState = false;
        restartColumn.ToolTipText = "Google Drive が一時ダウンした時に、アプリを強制終了させ、復旧した時にアプリを起動し直すかどうか";
        restartColumn.TrueValue = true;
        restartColumn.Width = 70;
        //
        // buttonBar
        //
        buttonBar.ColumnCount = 2;
        buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonBar.Controls.Add(buttonLayout, 1, 0);
        buttonBar.Dock = DockStyle.Fill;
        buttonBar.Name = "buttonBar";
        // 上下はセル内で中央寄せし、Padding で高さを削らない（見切れ防止）
        buttonBar.Padding = new Padding(AppLayout.Spacing, 0, AppLayout.Spacing, 0);
        buttonBar.RowCount = 1;
        buttonBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        buttonBar.TabIndex = 1;
        //
        // buttonLayout（右寄せ）
        //
        buttonLayout.Anchor = AnchorStyles.Right;
        buttonLayout.AutoSize = true;
        buttonLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        buttonLayout.Controls.Add(startAllButton);
        buttonLayout.Controls.Add(saveButton);
        buttonLayout.Controls.Add(cancelButton);
        buttonLayout.Margin = new Padding(0);
        buttonLayout.Name = "buttonLayout";
        buttonLayout.Padding = new Padding(0);
        buttonLayout.TabIndex = 0;
        buttonLayout.WrapContents = false;
        //
        // startAllButton
        //
        startAllButton.Enabled = false;
        startAllButton.Margin = new Padding(0, 0, AppLayout.Spacing, 0);
        startAllButton.Name = "startAllButton";
        startAllButton.Size = new Size(150, AppLayout.ButtonHeight);
        startAllButton.TabIndex = 0;
        startAllButton.Text = "TEST START ALL";
        startAllButton.TextAlign = ContentAlignment.MiddleCenter;
        startAllButton.UseVisualStyleBackColor = false;
        startAllButton.Click += StartAllButton_Click;
        //
        // saveButton
        //
        saveButton.Margin = new Padding(0, 0, AppLayout.Spacing, 0);
        saveButton.Name = "saveButton";
        saveButton.Size = new Size(110, AppLayout.ButtonHeight);
        saveButton.TabIndex = 1;
        saveButton.Text = "SAVE";
        saveButton.TextAlign = ContentAlignment.MiddleCenter;
        saveButton.UseVisualStyleBackColor = false;
        saveButton.Click += SaveButton_Click;
        //
        // cancelButton
        //
        cancelButton.Margin = new Padding(0);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new Size(110, AppLayout.ButtonHeight);
        cancelButton.TabIndex = 2;
        cancelButton.Text = "CANCEL";
        cancelButton.TextAlign = ContentAlignment.MiddleCenter;
        cancelButton.UseVisualStyleBackColor = false;
        cancelButton.Click += CancelButton_Click;
        //
        // gridContextMenu
        //
        gridContextMenu.Items.AddRange(new ToolStripItem[] { testRunMenuItem, deleteMenuItem });
        gridContextMenu.Name = "gridContextMenu";
        gridContextMenu.Opening += GridContextMenu_Opening;
        //
        // testRunMenuItem
        //
        testRunMenuItem.Name = "testRunMenuItem";
        testRunMenuItem.Text = "Test Run (&R)";
        testRunMenuItem.Click += TestRunMenuItem_Click;
        //
        // deleteMenuItem
        //
        deleteMenuItem.Name = "deleteMenuItem";
        deleteMenuItem.Text = "Delete (&D)";
        deleteMenuItem.Click += DeleteMenuItem_Click;
        //
        // MainForm
        //
        AllowDrop = true;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 280);
        Controls.Add(rootLayout);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "MainForm";
        SizeGripStyle = SizeGripStyle.Hide;
        StartPosition = FormStartPosition.Manual;
        Text = "MGA G Delay Run Setting - Version 1.0.0";
        DragDrop += EntryGrid_DragDrop;
        DragEnter += EntryGrid_DragEnter;
        rootLayout.ResumeLayout(false);
        optionsBar.ResumeLayout(false);
        optionsBar.PerformLayout();
        maxWaitRow.ResumeLayout(false);
        maxWaitRow.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)entryGrid).EndInit();
        buttonBar.ResumeLayout(false);
        buttonBar.PerformLayout();
        buttonLayout.ResumeLayout(false);
        gridContextMenu.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel rootLayout;
    private TableLayoutPanel optionsBar;
    private FlowLayoutPanel maxWaitRow;
    private Label maxWaitLabel;
    private TextBox maxWaitTextBox;
    private Label maxWaitUnitLabel;
    private CheckBox startMinimizedCheckBox;
    private DataGridView entryGrid;
    private DataGridViewTextBoxColumn delayColumn;
    private DataGridViewTextBoxColumn fileNameColumn;
    private DataGridViewTextBoxColumn pathColumn;
    private DataGridViewTextBoxColumn optionColumn;
    private DataGridViewCheckBoxColumn restartColumn;
    private TableLayoutPanel buttonBar;
    private FlowLayoutPanel buttonLayout;
    private AppButton startAllButton;
    private AppButton saveButton;
    private AppButton cancelButton;
    private ContextMenuStrip gridContextMenu;
    private ToolStripMenuItem testRunMenuItem;
    private ToolStripMenuItem deleteMenuItem;
}
