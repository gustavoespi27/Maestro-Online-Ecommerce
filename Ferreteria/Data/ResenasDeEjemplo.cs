namespace Ferreteria.Data;

/// <summary>
/// Reseñas de demostración (proyecto académico). Se muestran rotuladas como "Reseña de ejemplo"
/// para no inducir a error al consumidor (Ley 19.496, art. 28: publicidad engañosa).
/// Formato: SKU, autor, estrellas (1-5), comentario, hace cuántos días.
/// </summary>
public static class ResenasDeEjemplo
{
    public static readonly (string Sku, string Autor, int Estrellas, string Comentario, int DiasAtras)[] Lista =
    {
        // ---- Herramientas manuales ----
        ("HM-001", "Carlos M.", 5, "Buen martillo, pesa lo justo y el mango no vibra nada. Lo uso harto en la casa y ningún problema.", 12),
        ("HM-001", "Patricia R.", 4, "Firme y bien terminado. Le pongo 4 porque esperaba que fuera un poquito más liviano, pero cumple.", 30),
        ("HM-001", "Don Luis", 5, "Llevo 30 años de carpintero y este Stanley es de los buenos. Recomendado.", 48),
        ("HM-002", "Javiera S.", 5, "Trae todas las medidas que uno necesita para la casa. Llegó súper rápido a Ñuñoa.", 6),
        ("HM-002", "Felipe A.", 4, "Buena calidad por el precio, las puntas no se han gastado. El estuche podría ser mejor.", 22),
        ("HM-003", "Rodrigo V.", 5, "El aislante del mango se siente seguro, ideal para trabajos eléctricos chicos.", 15),
        ("HM-003", "Marcela P.", 4, "Corta bien el cable. Un poco duro al principio pero después se suelta.", 41),
        ("HM-004", "Andrés T.", 5, "El freno funciona perfecto y la cinta no se dobla. Para el precio, impecable.", 9),
        ("HM-004", "Camila F.", 3, "Cumple, pero el gancho se soltó un poco después de un mes. Para uso casero está bien.", 55),
        ("HM-005", "Mauricio G.", 5, "Bahco es otro nivel, la llave no se descalibra. Quedaban pocas y alcancé a comprar.", 4),

        // ---- Herramientas eléctricas ----
        ("HE-001", "Sebastián L.", 5, "Perforé hormigón sin problema. Con el descuento del Hot Sale quedó a muy buen precio.", 3),
        ("HE-001", "Valentina C.", 4, "Potente y fácil de usar, aunque hace bastante ruido. Llegó en 2 días a Maipú.", 18),
        ("HE-001", "Jorge E.", 5, "Ya es mi segundo Bosch. Le doy uso todos los días en la obra y aguanta.", 37),
        ("HE-001", "Daniela H.", 4, "Lo compré para colgar repisas y cuadros, súper práctico. Me hubiese gustado que trajera brocas.", 60),
        ("HE-002", "Ignacio B.", 5, "Las dos baterías hacen la diferencia, nunca te quedas sin carga. El maletín es muy cómodo.", 7),
        ("HE-002", "Francisca O.", 5, "Me lo recomendaron por el chat de la página y le achuntaron. Liviano y con buen torque.", 26),
        ("HE-002", "Cristián N.", 4, "Excelente atornillador. Le bajo una estrella porque el cargador es lento.", 44),
        ("HE-003", "Pablo R.", 5, "Corta fierro y cerámica sin drama. El protector se ajusta rápido sin herramientas.", 11),
        ("HE-003", "Esteban M.", 4, "Buen esmeril para el precio. Usen siempre lentes, que salta harta chispa.", 33),
        ("HE-004", "Hugo P.", 5, "Corte limpio y derecho, el disco que trae es bueno. Quedaban pocas unidades.", 8),
        ("HE-004", "Rocío A.", 3, "Funciona bien pero es pesada para mí. Para alguien con experiencia debe ser ideal.", 29),

        // ---- Pinturas ----
        ("PI-001", "Claudia V.", 5, "Rinde harto, con un galón pinté la pieza completa a dos manos. No deja olor fuerte.", 5),
        ("PI-001", "Matías D.", 4, "Buena cobertura y se lava fácil. Para el living quedó perfecto.", 20),
        ("PI-001", "Soledad I.", 5, "Aproveché la oferta y compré tres. El blanco queda bien parejo.", 52),
        ("PI-002", "Tomás F.", 4, "Brillo bonito sobre la reja. Hay que dejarlo secar bien entre manos.", 16),
        ("PI-003", "Gabriela M.", 5, "No gotea nada de verdad. Combina perfecto con el látex.", 10),
        ("PI-003", "René S.", 4, "Buen rodillo, el mango es firme. Suelta un poco de pelusa la primera vez.", 39),
        ("PI-004", "Constanza L.", 4, "Las brochas son buenas para el precio, la cerda no se cae.", 24),

        // ---- Gasfitería ----
        ("GA-001", "Alejandro K.", 5, "Bronce de verdad, se nota la calidad. La instalé yo mismo siguiendo un tutorial.", 13),
        ("GA-001", "Paula Q.", 4, "Funciona bien, cierra suave. Llegó bien embalada.", 47),
        ("GA-002", "Diego C.", 5, "Teflón de toda la vida, barato y cumple. Siempre hay que tener en la casa.", 2),
        ("GA-003", "Lorena B.", 5, "Quedó preciosa la cocina. El caño gira bien y el agua sale con buena presión.", 14),
        ("GA-003", "Marco T.", 4, "Bonito y fácil de instalar. Las mangueras que trae son un poco cortas.", 35),
        ("GA-003", "Isabel G.", 2, "El modelo es bonito, pero el mío venía con una pieza mal ajustada. Igual me lo cambiaron rápido por WhatsApp.", 58),

        // ---- Electricidad ----
        ("EL-001", "Fernando R.", 5, "Cable certificado y los 100 metros completos. Para instalaciones de casa, excelente.", 19),
        ("EL-002", "Natalia P.", 5, "Compré 10 para toda la casa y bajó harto la cuenta de la luz. Luz cálida muy agradable.", 1),
        ("EL-002", "Gonzalo A.", 5, "A este precio no hay que pensarlo. Encienden al tiro.", 21),
        ("EL-002", "Bárbara S.", 4, "Buenas ampolletas, una vino un poco más amarilla que las otras pero nada grave.", 40),
        ("EL-003", "Víctor H.", 4, "Fácil de instalar, quedó firme. Las tapas blancas combinan con todo.", 27),

        // ---- Fijaciones ----
        ("FI-001", "Ricardo M.", 5, "Entran fácil en el perfil, no se barren. Para volcanita son los mejores.", 17),
        ("FI-002", "Juan Carlos E.", 4, "Clavos derechitos y buen kilo. Sirven para todo tipo de trabajo.", 32),
        ("FI-003", "Antonia V.", 5, "Agarran firme en ladrillo. Colgué un mueble pesado y ni se mueve.", 23),
        ("FI-003", "Héctor L.", 4, "Buenos tarugos, quedaban 2 cajas y pedí las dos. Ojalá repongan.", 9),

        // ---- Construcción ----
        ("CO-001", "Raúl P.", 5, "Buen cemento, fragua parejo. El despacho a la obra llegó en el día que decían.", 25),
        ("CO-001", "Mónica F.", 4, "Llegaron bien los sacos, sin romperse. El repartidor muy amable.", 50),
        ("CO-002", "Óscar D.", 5, "Pegué cerámica en el baño y quedó firme. Rinde lo que dice el saco.", 28),

        // ---- Jardín ----
        ("JA-001", "Carolina N.", 5, "No se dobla ni se enreda como las baratas. Los conectores son buenos.", 6),
        ("JA-001", "Eduardo J.", 4, "Buena manguera, aunque 15 metros me quedaron un poco justos para el patio.", 31),
        ("JA-001", "Pamela R.", 5, "Excelente compra con el descuento. Mi papá feliz regando sus plantas.", 45),
        ("JA-002", "Teresa M.", 5, "Corta las ramas como mantequilla, cómoda en la mano. Muy recomendada.", 12),
        ("JA-002", "Arturo S.", 4, "Buena tijera, el resorte es firme. Hay que secarla después de usar.", 36),
    };
}
