using System.Windows.Controls;
using System.Windows.Media;

namespace VESCO
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

        public Button _selectToolButton { get; }
        public Button _cutToolButton { get; }


        public ToolManager(Button selectTool, Button cutTool)
        {
            _selectToolButton = selectTool;
            _cutToolButton = cutTool;
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
            _selectToolButton.BorderBrush = _activeTool == ToolType.Select ? Brushes.Blue : Brushes.Black;
            _cutToolButton.BorderBrush = _activeTool == ToolType.Cut ? Brushes.Blue : Brushes.Black;
        }

        private void ClearAllHighlights()
        {
            _selectToolButton.BorderBrush = Brushes.Black;
            _cutToolButton.BorderBrush = Brushes.Black;
        }
    }
}