window.EXERCISES = [
  {
    id: "ere-1-digits",
    title: "Nivel 1 (ERE): detectar números (solo dígitos)",
    prompt: "Escribe una ERE que haga match cuando una línea contenga al menos un dígito (0-9).",
    dialect: "ERE",
    hints: [
      "Piensa en una clase de caracteres que represente los dígitos.",
      "La clase [0-9] o [[:digit:]] matchea cualquier dígito. No necesitas anclar.",
      "Solución: [0-9]"
    ],
    tests: [
      ["hola", false], ["id=7", true], ["123", true], ["sin_numeros_aqui", false],
      ["", false], ["v1.2.3", true], ["\t42\t", true], ["_0_", true],
      ["π", false], ["007", true], ["NaN", false], ["---", false], ["x\ty5z", true]
    ]
  },
  {
    id: "ere-1-starts-with-digit",
    title: "Nivel 1 (ERE): primer carácter es dígito",
    prompt: "Escribe una ERE que haga match SOLO si el primer carácter de la línea es un dígito (0-9). Puede haber más texto después.",
    dialect: "ERE",
    hints: [
      "Necesitas indicar que la línea empieza con algo concreto.",
      "Usa ^ para anclar al inicio, seguido de la clase de dígitos.",
      "Solución: ^[0-9]"
    ],
    tests: [
      ["1abc", true], ["0", true], ["9z", true], ["a1", false], [" 1", false],
      ["", false], ["\t2", false], ["42 respuestas", true]
    ]
  },
  {
    id: "ere-1-anchors-only-digits",
    title: "Nivel 1 (ERE): anclas ^$ (solo dígitos)",
    prompt: "Escribe una ERE que haga match SOLO si toda la línea está formada por dígitos. Pista: usa ^ y $.",
    dialect: "ERE",
    hints: [
      "Para que 'toda la línea' sea dígitos, necesitas anclar inicio y fin.",
      "Combina ^ al inicio, [0-9]+ en medio, y $ al final.",
      "Solución: ^[0-9]+$"
    ],
    tests: [
      ["123", true], ["0010", true], ["0", true], ["id=7", false], ["7 ", false],
      [" 7", false], ["", false], ["12a34", false], ["12.34", false],
      ["00000000000", true], ["12e3", false]
    ]
  },
  {
    id: "ere-2-email-like",
    title: "Nivel 2 (ERE): detectar 'algo@algo' (simplificado)",
    prompt: "Escribe una ERE que haga match si existe un patrón tipo usuario@dominio (simplificado, sin validar RFC).",
    dialect: "ERE",
    hints: [
      "Busca un patrón que tenga algo antes de @ y algo después.",
      "Usa [^@]+ para el usuario (uno o más que no sean @) y [^@]+ para el dominio.",
      "Solución: [^@]+@[^@]+"
    ],
    tests: [
      ["contacto@ejemplo.com", true], ["sin arroba", false], ["a@b", true],
      ["@dominio.com", false], ["@@", false], ["user.name+tag@host", true],
      ["solo@", false], ["@", false], ["x@y", true], ["mail@a.co", true],
      ["bad@@here", false], ["name@sub.domain.org", true]
    ]
  },
  {
    id: "ere-2-ends-with-com",
    title: "Nivel 2 (ERE): termina en .com",
    prompt: "Escribe una ERE que haga match SOLO si la línea termina literalmente en '.com' (punto + com). Mayúsculas/minúsculas exactas en 'com'.",
    dialect: "ERE",
    hints: [
      "Necesitas que la línea termine en algo específico. ¿Qué ancla usas?",
      "Escapa el punto con \\., ya que sin escape '.' es cualquier carácter.",
      "Solución: \\.com$"
    ],
    tests: [
      ["ejemplo.com", true], ["a.com", true], ["ejemplo.comm", false],
      ["ejemplo.co", false], ["com", false], [".com", true],
      ["ejemplo.Com", false], ["ejemplo.com ", false], [" sub.ejemplo.com", true]
    ]
  },
  {
    id: "ere-2-alternation-cat-dog",
    title: "Nivel 2 (ERE): alternancia | (cat o dog)",
    prompt: "Escribe una ERE que haga match si una línea contiene 'cat' o 'dog' como subcadena.",
    dialect: "ERE",
    hints: [
      "Necesitas buscar una de dos palabras. ¿Qué operador de ERE permite elegir entre alternativas?",
      "Usa | (pipe) para alternancia: cat|dog. No necesitas anclar.",
      "Solución: cat|dog"
    ],
    tests: [
      ["cat", true], ["dog", true], ["hotdog", true], ["caterpillar", true],
      ["do g", false], ["bird", false], ["CAT", false], ["scatter", true],
      ["the dog ran", true], ["", false], ["catalog", true], ["dogma", true],
      ["catdog", true]
    ]
  },
  {
    id: "ere-2-alternation-yes-no-line",
    title: "Nivel 2 (ERE): toda la línea = yes/no pegados (grupo y +)",
    prompt: "Escribe una ERE que haga match SOLO si toda la línea está formada por una o más repeticiones pegadas de las subcadenas literales yes o no (solo minúsculas). Válidos: yes, no, yesno, nonoyes, yesyes. No puede haber espacios ni otros caracteres. Debes agrupar yes|no entre paréntesis y cuantificar ese grupo (p. ej. +) y anclar toda la línea con ^ y $.",
    dialect: "ERE",
    hints: [
      "Necesitas combinar yes y no en cualquier orden, repetidos. Primero agrupa las alternativas.",
      "Agrupa (yes|no) entre paréntesis. Luego cuantifica el grupo con + (una o más veces).",
      "Solución: ^(yes|no)+$"
    ],
    tests: [
      ["yes", true], ["no", true], ["yesno", true], ["nonoyes", true],
      ["yesyes", true], ["noyes", true], ["yeyes", false], ["", false],
      ["yes ", false], [" yes", false], ["YES", false], ["maybe", false],
      ["yesx", false], ["non", false], ["ye", false], ["on", false],
      ["yes-yes", false]
    ]
  },
  {
    id: "ere-3-backref-double-char",
    title: "Nivel 3 (ERE): dos caracteres idénticos (referencia \\1)",
    prompt: "Escribe una ERE anclada (^ y $) tal que toda la línea sea exactamente DOS caracteres iguales. Usa un primer carácter capturado entre ( ) y repítelo con \\1 (misma captura). Cualquier carácter vale (letra, dígito, espacio…).",
    dialect: "ERE",
    hints: [
      "Necesitas capturar un carácter y luego referenciarlo. Usa grupos con ( ).",
      "Captura cualquier carácter con (.) y repite con \\1. No olvides las anclas ^ y $.",
      "Solución: ^(.)\\1$"
    ],
    tests: [
      ["aa", true], ["zz", true], ["99", true], ["  ", true],
      ["ab", false], ["a", false], ["aaa", false], ["", false],
      ["a\t", false], ["\t\t", true]
    ]
  },
  {
    id: "ere-3-backref-word-hyphen",
    title: "Nivel 3 (ERE): palabra-palabra repetida (\\1)",
    prompt: "Escribe una ERE con ^ y $: la línea debe ser una palabra solo en minúsculas [a-z]+, un guion -, y la MISMA palabra otra vez (referencia \\1). Ejemplos válidos: cat-cat, go-go, a-a. No: cat-dog, go-goo, Cat-cat.",
    dialect: "ERE",
    hints: [
      "Necesitas capturar una palabra de minúsculas y repetirla después del guion.",
      "Captura ([a-z]+), luego el guion literal -, y repite con \\1.",
      "Solución: ^([a-z]+)-\\1$"
    ],
    tests: [
      ["cat-cat", true], ["go-go", true], ["a-a", true], ["zoo-zoo", true],
      ["cat-dog", false], ["go-goo", false], ["cat", false],
      ["cat-cat-cat", false], ["Cat-cat", false], ["", false]
    ]
  },
  {
    id: "ere-3-posix-class-alpha",
    title: "Nivel 3 (ERE): clases POSIX [[:alpha:]]",
    prompt: "Escribe una ERE que haga match si la línea contiene al menos una letra ASCII (a-z/A-Z). Pista: usa [[:alpha:]].",
    dialect: "ERE",
    hints: [
      "Las clases POSIX se escriben entre dobles corchetes: [[:nombre:]].",
      "[[:alpha:]] representa letras. No necesitas anclar, solo que aparezca al menos una.",
      "Solución: [[:alpha:]]"
    ],
    tests: [
      ["123", false], ["abc", true], ["ABC", true], ["id=7", true],
      ["___", false], ["123.", false], ["___a___", true], ["\tZ\t", true],
      ["", false], ["123x456", true], ["___m___", true], ["[[", false]
    ]
  },
  {
    id: "ere-3-posix-space",
    title: "Nivel 3 (ERE): contiene espacio en blanco POSIX",
    prompt: "Escribe una ERE que haga match si la línea contiene al menos un carácter de espacio en blanco según [[:space:]] (espacio, tab, etc.).",
    dialect: "ERE",
    hints: [
      "Existe una clase POSIX para caracteres de espacio en blanco.",
      "[[:space:]] detecta espacios, tabs, saltos de línea, etc.",
      "Solución: [[:space:]]"
    ],
    tests: [
      ["a b", true], ["sin", false], ["\tunico", true],
      ["sin-espacio", false], ["x\ty", true], ["", false], ["___", false]
    ]
  },
  {
    id: "ere-3-negated-class-no-digits",
    title: "Nivel 3 (ERE): negación [^...] (sin dígitos)",
    prompt: "Escribe una ERE que haga match SOLO si la línea NO contiene ningún dígito.",
    dialect: "ERE",
    hints: [
      "Necesitas que toda la línea esté compuesta por caracteres que NO sean dígitos.",
      "Usa una clase negada [^0-9] o [^[:digit:]] y ancla con ^ y $.",
      "Solución: ^[^0-9]*$"
    ],
    tests: [
      ["hola", true], ["id=7", false], ["abc123", false], ["___--__", true],
      ["", true], ["solo\tguiones-y_", true], ["0", false], ["a0b", false],
      [" \t", true], ["a|b", false], ["no-digits-here", true], ["unicode∞", true]
    ]
  },
  {
    id: "ere-4-quantifiers-range",
    title: "Nivel 4 (ERE): cuantificadores {m,n} (2 a 4 dígitos)",
    prompt: "Escribe una ERE que haga match SOLO si la línea es un número de 2 a 4 dígitos (sin espacios).",
    dialect: "ERE",
    hints: [
      "Necesitas controlar cuántos dígitos pueden aparecer (un rango).",
      "Usa {2,4} como cuantificador de rango después de [0-9].",
      "Solución: ^[0-9]{2,4}$"
    ],
    tests: [
      ["7", false], ["12", true], ["999", true], ["2026", true],
      ["12345", false], ["12 ", false], ["01", true], ["000", true],
      ["12x", false], ["", false], ["0000", false], ["10", true], ["+99", false]
    ]
  },
  {
    id: "ere-4-only-letter-a",
    title: "Nivel 4 (ERE): solo letras a (una o más)",
    prompt: "Escribe una ERE que haga match SOLO si toda la línea está formada por una o más letras 'a' minúscula (sin otros caracteres).",
    dialect: "ERE",
    hints: [
      "Solo puede haber la letra 'a' repetida una o más veces. ¿Qué cuantificador usas?",
      "Usa a+ para una o más 'a', y ancla con ^ y $ para que no haya nada más.",
      "Solución: ^a+$"
    ],
    tests: [
      ["a", true], ["aaa", true], ["", false], ["aaA", false],
      ["aab", false], [" a", false], ["aa ", false]
    ]
  },
  {
    id: "ere-5-simple-phone",
    title: "Nivel 5 (ERE): patrón completo (teléfono 000-000-000)",
    prompt: "Escribe una ERE que haga match SOLO si la línea tiene el formato 000-000-000 (3 dígitos, guion, 3 dígitos, guion, 3 dígitos).",
    dialect: "ERE",
    hints: [
      "El formato es fijo: tres grupos de 3 dígitos separados por guiones.",
      "Usa [0-9]{3} para cada grupo y - literal entre ellos. Ancla con ^ y $.",
      "Solución: ^[0-9]{3}-[0-9]{3}-[0-9]{3}$"
    ],
    tests: [
      ["123-456-789", true], ["123456789", false], ["123-45-6789", false],
      ["123-456-789 ", false], ["012-000-999", true], ["12-345-678", false],
      ["123-456-78a", false], ["000-000-000", true], ["123-456-7890", false],
      ["999-888-777", true], ["123_456_789", false]
    ]
  },
  {
    id: "ere-5-host-port",
    title: "Nivel 5 (ERE): host:puerto (simple)",
    prompt: "Escribe una ERE que haga match SOLO si toda la línea tiene forma HOST:PORT donde HOST es letras minúsculas (a-z) de 3 a 6 caracteres y PORT es exactamente 2 a 4 dígitos.",
    dialect: "ERE",
    hints: [
      "Divide el patrón en tres partes: HOST, los dos puntos literales, y PORT.",
      "HOST: [a-z]{3,6}, los dos puntos :, PORT: [0-9]{2,4}. Ancla todo con ^ y $.",
      "Solución: ^[a-z]{3,6}:[0-9]{2,4}$"
    ],
    tests: [
      ["api:80", true], ["tests:8080", true], ["db:443", false],
      ["dbs:443", true], ["API:80", false], ["ab:80", false],
      ["toolonghostname:80", false], ["host:8", false], ["host:80808", false],
      ["host:80a", false], ["host80", false]
    ]
  },
  {
    id: "bre-1-literal-plus",
    title: "Nivel 1 (BRE): '+' es literal",
    prompt: "En BRE, el carácter '+' NO es cuantificador. Haz match en líneas que contengan literalmente 'C++'.",
    dialect: "BRE",
    hints: [
      "En BRE (grep sin -E), los caracteres especiales como + son literales.",
      "Solo necesitas escribir C++ tal cual, sin escapar nada.",
      "Solución: C++"
    ],
    tests: [
      ["Me gusta C++", true], ["Me gusta C+", false], ["C--", false],
      ["C++++", true], ["C+", false], ["prefijo C++ sufijo", true],
      ["c++", false], ["", false], ["C++11", true]
    ]
  },
  {
    id: "bre-2-escaped-plus-quantifier",
    title: "Nivel 2 (BRE): \\+ como cuantificador (GNU grep)",
    prompt: "En muchos grep (GNU), en BRE puedes usar \\+ como 'uno o más'. Haz match si la línea contiene al menos un dígito usando BRE + \\+.",
    dialect: "BRE",
    hints: [
      "En BRE con GNU grep, para hacer que + sea cuantificador debes escaparlo.",
      "Usa \\+ en lugar de + para indicar 'una o más repeticiones'.",
      "Solución: [0-9]\\+"
    ],
    tests: [
      ["hola", false], ["id=7", true], ["123", true], ["sin_numeros", false],
      ["", false], ["x0x", true], ["0", true], ["a\tb", false],
      ["999999", true], ["_8_", true], ["42e10", true]
    ]
  },
  {
    id: "bre-3-literal-dot",
    title: "Nivel 3 (BRE): punto literal \\.",
    prompt: "En BRE, '.' coincide con cualquier carácter. Haz match en líneas que contengan un punto literal seguido de la subcadena 'txt' (como en nombres de fichero). Usa '\\.' para el punto literal.",
    dialect: "BRE",
    hints: [
      "En BRE, . es cualquier carácter. Para buscar un punto real, necesitas escaparlo.",
      "Escapa el punto con \\. y después escribe txt. No necesitas anclar.",
      "Solución: \\.txt"
    ],
    tests: [
      ["readme.txt", true], ["config.txtext", true], ["txttxt", false],
      ["readme-md", false], [".txt", true], ["readme.txt ", true],
      ["txt", false], ["a.txtx", true]
    ]
  },
  {
    id: "bre-4-backref-word-hyphen",
    title: "Nivel 4 (BRE): palabra-palabra repetida con \\1",
    prompt: "En BRE, agrupa con \\( \\) y repite con \\1. Para 'una o más' letras minúsculas en GNU grep usa \\+. Haz match SOLO si toda la línea es: [a-z]+, guion, la misma palabra otra vez (mismo patrón que en ERE pero escapado).",
    dialect: "BRE",
    hints: [
      "En BRE, los paréntesis y el + necesitan escapes: \\( \\) y \\+.",
      "Captura \\([a-z]\\+\\), luego el guion -, y repite con \\1. Ancla con ^ y $.",
      "Solución: ^\\([a-z]\\+\\)-\\1$"
    ],
    tests: [
      ["cat-cat", true], ["go-go", true], ["a-a", true], ["zoo-zoo", true],
      ["cat-dog", false], ["go-goo", false], ["cat", false],
      ["cat-cat-cat", false], ["Cat-cat", false], ["", false]
    ]
  }
];
