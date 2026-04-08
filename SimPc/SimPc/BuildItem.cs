using SimPc;

public class BuildItem
{
    public Product Product { get; set; }
    public string Category { get; set; }
    public string CategoryIcon
    {
        get
        {
            switch (Category)
            {
                case "Процессоры": return "⚡";
                case "Видеокарты": return "🎮";
                case "Материнские платы": return "🔌";
                case "Оперативная память": return "💾";
                case "Накопители": return "💿";
                case "Блоки питания": return "⚡";
                case "Корпуса": return "📦";
                default: return "🖥️";
            }
        }
    }
}