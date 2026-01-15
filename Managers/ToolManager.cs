using System.Windows.Controls;
using System.Windows.Media;

namespace VESCO.Managers
{
    public enum ToolType
    {
        None,
        Select,
        Cut
    }

    public class ToolManager
    {
        private ToolType _activeTool = ToolType.None;

        public ToolType ActiveTool => _activeTool;

        public Border _selectTool { get; }
        public Border _cutTool { get; }

        public ToolManager(Border selectTool, Border cutTool)
        {
            _selectTool = selectTool;
            _cutTool = cutTool;
        }

        public void ToggleTool(ToolType tool)
        {
            if (_activeTool == tool)
            {
                _activeTool = ToolType.None;
                ClearAllHighlights();
            }
            else
            {
                _activeTool = tool;
                UpdateToolHighlights();
            }
        }

        private void UpdateToolHighlights()
        {
            _selectTool.BorderBrush = _activeTool == ToolType.Select ? Brushes.Blue : Brushes.Black;
            _cutTool.BorderBrush = _activeTool == ToolType.Cut ? Brushes.Blue : Brushes.Black;
        }

        private void ClearAllHighlights()
        {
            _selectTool.BorderBrush = Brushes.Black;
            _cutTool.BorderBrush = Brushes.Black;
        }
    }
}