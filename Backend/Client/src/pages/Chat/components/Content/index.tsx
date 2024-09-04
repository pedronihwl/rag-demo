import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { useApi } from "@/services/useApi";
import { useMutation } from "@tanstack/react-query";
import clsx from "clsx";
import { AppWindowIcon, Loader2 } from "lucide-react";
import { useState } from "react";
import { useParams } from "react-router-dom";

type Answer = {
    answer: string;
    fonts: string[];
    files: string[];
}

type Request = {
    role: string;
    content: string | Answer;
}

function isAnswer(content: string | Answer): content is Answer {
    return typeof content === "object" && "answer" in content;
}

type IProps = {
    cb: (answer: Answer) => void;
}

const Content = ({ cb }: IProps) => {
    const { id } = useParams()
    const [history, setHistory] = useState<Request[]>([])

    const [value,setValue] = useState('')
    const { api } = useApi()
    const { mutate, isPending } = useMutation({
        mutationFn: async(question: string) => {
            let data = history.map(item => ({
                role: item.role,
                content: isAnswer(item.content) ? item.content.answer : item.content
            }))

            data = [...data, { role: 'user', content: question }]

            setHistory(data)

            const response = await api.request<Answer>({
                url: `/chat/${id}`,
                method: 'POST',
                data: {
                    history: data
                }
            })

            return response;
        },
        onSuccess: (data) => {
            setHistory([...history, { role: 'assistant', content: data }])
            cb(data)
        }
    })

    const onChat = () => {
        setValue('')
        mutate(value)
    }

    return <div className="h-full relative">
        <div className="flex-1 overflow-y-auto p-4">
            {history.map((msg, index) => (
                <div key={index} className={clsx('flex mb-4', {
                    'justify-end': msg.role === "user",
                    'justify-start': msg.role === "assistant"
                })}>
                    <div
                        className={clsx('p-3 rounded-lg max-w-xs', {
                            "bg-blue-500 text-white": msg.role === "user",
                            "bg-gray-300 text-gray-800": msg.role === "assistant"
                        })}
                    >
                        {typeof msg.content === "object" && "answer" in msg.content ? (msg.content as Answer).answer : msg.content}
                    </div>
                </div>
            ))}
        </div>
        <div className="p-4 absolute bottom-0 w-full">
            <Textarea placeholder="Prompt" className="w-full mb-2"  value={value} onChange={e => setValue(e.target.value)} />
            <Button disabled={isPending} variant="secondary" className="w-full" onClick={onChat}>
                {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin"/>}
                Perguntar
            </Button>
        </div>
    </div>
}

export default Content;