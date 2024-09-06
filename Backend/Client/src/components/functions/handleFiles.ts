import { DbFile } from "@/types/@types";

const handleFiles = (fileList: FileList, action: React.Dispatch<React.SetStateAction<any[]>>) => {
    const auxFiles : Partial<DbFile>[]= [];

    for (let i = 0; i < fileList.length; i++) {
        const file = fileList[i];

        if (file.type !== 'application/pdf') {
            continue
        }

        const fileName = file.name
            .replace(/[^\w\s.]/gi, "")
            .replace(/\s+/g, "_")
            .toLowerCase();

        auxFiles.push({
            hash: fileName,
            stream: file,
            mime: file.type,
            size: (file.size / (1024 * 1024)),
            id: `${i}`
        });
    }

    action(auxFiles)
};

export {
    handleFiles
}