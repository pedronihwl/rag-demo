import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { useApi } from "@/services/useApi";
import { Answer } from "@/types/@types";
import { useMutation } from "@tanstack/react-query";
import { ScrollArea } from "@/components/ui/scroll-area"
import clsx from "clsx";
import { Loader2 } from "lucide-react";
import { useRef, useState } from "react";
import { useParams } from "react-router-dom";

type Request = {
    role: string;
    content: string | Answer;
}

function isAnswer(content: string | Answer): content is Answer {
    return typeof content === "object" && "answer" in content;
}

type IProps = {
    isDisabled: boolean
    cb: (answer: Answer) => void;
}

const Content = ({ cb, isDisabled }: IProps) => {
    const { id } = useParams()
    const [history, setHistory] = useState<Request[]>([])

    const scroll = useRef<HTMLDivElement | null>(null);

    const [value, setValue] = useState('')
    const { api } = useApi()
    const { mutate, isPending } = useMutation({
        mutationFn: async (question: string) => {
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

        if (scroll.current) {
            scroll.current.scrollIntoView({ behavior: 'smooth' });
        }
    }

    return <div className="h-full relative">
        <ScrollArea ref={scroll} className="h-[calc(100%-80px)] p-4 pb-14">
            {history.map((msg, index) => (
                <div key={index} className={clsx('flex mb-4', {
                    'justify-end': msg.role === "user",
                    'justify-start': msg.role === "assistant"
                })}>
                    <div
                        className={clsx('p-3 rounded-lg max-w-sm', {
                            "bg-blue-700 text-white": msg.role === "user",
                            "bg-gray-300 text-gray-800": msg.role === "assistant"
                        })}
                    >
                        {typeof msg.content === "object" && "answer" in msg.content ? (msg.content as Answer).answer : msg.content}
                    </div>
                </div>
            ))}
        </ScrollArea>
        <div className="p-4 absolute bottom-0 w-full">
            <Textarea disabled={isDisabled} placeholder="Prompt" className="w-full mb-2" value={value} onChange={e => setValue(e.target.value)} onKeyDown={e => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    onChat();
                }
            }} />
            <Button disabled={isPending || isDisabled} variant="secondary" className="w-full" onClick={onChat}>
                {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Perguntar
            </Button>
        </div>
    </div>
}

export default Content;