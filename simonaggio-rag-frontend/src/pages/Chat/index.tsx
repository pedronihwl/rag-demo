import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { fileMocks } from "@/utils/fake";
import { Copy, Upload } from "@phosphor-icons/react";
import { useRef } from "react";
import { useParams } from "react-router-dom";
import FileContainer from "./components/FileContainer";
import { Textarea } from "@/components/ui/textarea";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";


const Chat = () => {
    const { id } = useParams()
    const inputRef = useRef<HTMLInputElement>(null);

    return <div className="flex w-full h-full">
        <aside className="bg-zinc-900 p-4 h-full min-w-80 ">
            <Badge variant="secondary" className="cursor-pointer w-full flex justify-between py-2">{id}<Copy size={18} className="ml-2" /></Badge>

            <div>
                <Button variant="secondary" className="w-full my-4">
                    <input accept=".pdf" ref={inputRef} style={{ display: "none" }} type="file" multiple={true}></input>
                    <Upload size={32} className="mr-2 h-4 w-4" /> Novo documento
                </Button>

                <div className="flex flex-col gap-4 mt-2">
                    {fileMocks.map(item => (<FileContainer file={item} key={item.name} onDelete={file => console.log('file', file)} />))}
                </div>
            </div>
        </aside>
        <section className="flex w-full gap-4">
            <div className="flex-1"></div>
            <div className="flex-1 flex flex-col h-full">
                <div className="text-white mx-auto flex flex-1 gap-4 text-base md:gap-5 lg:gap-6 md:max-w-3xl lg:max-w-[40rem] xl:max-w-[48rem]">
                    <div className="flex-shrink-0 flex flex-col relative items-end">
                        <Avatar>
                            <AvatarImage src="https://github.com/shadcn.png" alt="@shadcn" />
                            <AvatarFallback>SM</AvatarFallback>
                        </Avatar>
                    </div>
                    <div className="relative flex w-full min-w-0 flex-col">1231313131312131231 sda d12 </div>
                </div>
                <div className="flex flex-col gap-2 mt-auto p-2">
                    <Textarea placeholder="Prompt" />
                    <Button variant="secondary">Enviar</Button>
                </div>
            </div>
        </section>

    </div>

}

export default Chat;