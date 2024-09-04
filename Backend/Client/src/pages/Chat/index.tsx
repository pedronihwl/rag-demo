import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { fileMocks } from "@/utils/fake";
import { Copy, Upload } from "@phosphor-icons/react";
import { useRef, useState } from "react";
import { useParams } from "react-router-dom";
import FileContainer from "./components/FileContainer";
import { Textarea } from "@/components/ui/textarea";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { useQuery } from "@tanstack/react-query";
import { useApi } from "@/services/useApi";
import { DbContext } from "@/types/@types";
import Content from "./components/Content";


const Chat = () => {
    const { api } = useApi()
    const { id } = useParams()
    const inputRef = useRef<HTMLInputElement>(null);


    const [question, setQuestion] = useState('')

    const { data, isError } = useQuery({
        queryFn: async () => {
            return api.request<DbContext>({
                url: `/context/${id}`
            })
        },
        queryKey: ['context']
    })

    console.log('the data: ', data)
    if(!data) return <div>Carregando...</div>

    if(isError) return <div>404</div>


    return <div className="flex w-full h-full">
        <aside className="p-4 h-full min-w-80">
            <Badge variant="secondary" className="cursor-pointer w-full flex justify-between py-2">{data.id}<Copy size={18} className="ml-2" /></Badge>
            <div>
                <Button variant="secondary" className="w-full my-4">
                    <input accept=".pdf" ref={inputRef} style={{ display: "none" }} type="file" multiple={true}></input>
                    <Upload size={32} className="mr-2 h-4 w-4" /> Novo documento
                </Button>

                <div className="flex flex-col gap-4 mt-2">
                    {data.files.map(item => (<FileContainer file={item} key={item.name} onDelete={file => console.log('file', file)} />))}
                </div>
            </div>
        </aside>
        <section className="flex w-full gap-4 border border-rose-600">
            <div className="flex-1 text-white border border-yellow-600">Pdf Viewer</div>
            <div className="flex-1">
                <Content/>
            </div>
        </section>

    </div>

}

export default Chat;