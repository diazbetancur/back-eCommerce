using CC.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CC.Domain.Entities
{
    public class ProductProperty : EntityBase<Guid>
    {
        public Guid Id { get; set; }
        public string Name { get; set; }          // "Color", "Talla", "Material"
        public string DisplayName { get; set; }   // "Color del producto"
        public PropertyType Type { get; set; }    // Text, Number, Boolean, Select
        public bool IsFilterable { get; set; }    // ¿Aparece en filtros?
        public bool IsSearchable { get; set; }    // ¿Se incluye en búsqueda?
        public int FilterOrder { get; set; }      // Orden en filtros
        public List<string> PredefinedValues { get; set; } // Para dropdowns
        public string Unit { get; set; }
    }
}