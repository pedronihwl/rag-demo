
const conversation: any[] = [
    { role: 'user', content: 'Olá, pode me ajudar com um problema de programação?' },
    { role: 'assistant', content: 'Claro! Em que posso te ajudar?' },
    { role: 'user', content: 'Estou tentando fazer um filtro dinâmico em JavaScript.' },
    {
        role: 'assistant',
        content: {
            answer: 'Você pode usar a função `filter()` para criar filtros dinâmicos. Aqui está um exemplo simples:',
            fonts: ['https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Array/filter'],
            files: []
        }
    },
    { role: 'user', content: 'Obrigado! E como eu posso combinar múltiplos filtros?' },
    {
        role: 'assistant',
        content: {
            answer: 'Você pode encadear múltiplas chamadas `filter()` ou combinar condições dentro de um único filtro.',
            fonts: ['https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Array/filter#multiple_filters'],
            files: []
        }
    },
    { role: 'user', content: 'Pode me mostrar um exemplo com múltiplos filtros encadeados?' },
    {
        role: 'assistant',
        content: {
            answer: 'Claro! Aqui está um exemplo de como encadear filtros para um array de números:',
            fonts: [],
            files: ['filterExample.js']
        }
    },
    { role: 'user', content: 'Ótimo! Isso ajudou muito. Muito obrigado!' },
    { role: 'assistant', content: 'De nada! Se precisar de mais alguma coisa, estou aqui para ajudar.' },
    { role: 'user', content: 'Pode me mostrar um exemplo com múltiplos filtros encadeados?' },
    {
        role: 'assistant',
        content: {
            answer: 'Claro! Aqui está um exemplo de como encadear filtros para um array de números:',
            fonts: [],
            files: ['filterExample.js']
        }
    },
    { role: 'user', content: 'Ótimo! Isso ajudou muito. Muito obrigado!' },
    { role: 'assistant', content: 'De nada! Se precisar de mais alguma coisa, estou aqui para ajudar.' }
];

export { conversation } 