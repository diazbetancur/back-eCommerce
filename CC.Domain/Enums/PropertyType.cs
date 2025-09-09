using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CC.Domain.Enums
{
    public enum PropertyType
    {
        Text = 1,           // Campo de texto libre
        Number = 2,         // Números (precio, peso, etc.)
        Boolean = 3,        // Sí/No (ej: ¿Tiene garantía?)
        SingleSelect = 4,   // Lista desplegable (una opción)
        MultiSelect = 5,    // Lista múltiple (varias opciones)
        Date = 6,           // Fechas
        Color = 7,          // Selector de color
        Range = 8,          // Rango numérico (ej: precio de 100-500)
        Email = 9,          // Validación de email
        Url = 10,           // URLs
        TextArea = 11,      // Texto largo (descripciones)
        Currency = 12,      // Moneda con formato
        Percentage = 13     // Porcentajes
    }
}