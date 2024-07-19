using System;

namespace DTO.Cliente
{
    public class ClienteToListDTO
    {
        public Guid Id_Empresa { get; set; }
        public Guid Id_Sucursal { get; set; }
        public Guid Id_Cliente { get; set; }
        public int? Numero_Cliente { get; set; }
        public string? Nombre { get; set; }         // Permitir nulos
        public string? Apellido { get; set; }       // Permitir nulos
        public int? Nro_Doc { get; set; }
        public string? Tipo_Doc { get; set; }       // Permitir nulos
        public string? Estado_Civil { get; set; }   // Permitir nulos
        public DateTime? Fecha_Nacimiento { get; set; }
        public string? Sexo { get; set; }           // Permitir nulos
        public string? Email { get; set; }          // Permitir nulos
        public string? Nacionalidad { get; set; }   // Permitir nulos
        public DateTime Fecha_Alta_Cliente { get; set; }
        public bool Estado { get; set; }
    }
}
